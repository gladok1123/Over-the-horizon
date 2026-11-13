using System.Numerics;
using System.Runtime.CompilerServices;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Renders dynamic 3D world overlays (Day/Sunset/Night Sun + Moon + Stars + Clouds, sub-voxel selection box,
/// 6-face <c>destroy_stage_0..9</c> crack overlays, <c>steve.png</c> Remote Players, articulated 3D mobs,
/// physics <c>PrimedTnt</c>, first-person Right-Hand &amp; Offhand viewmodels, and the 3D Inventory Steve preview)
/// alongside <see cref="SilkOpenGlUiRenderer"/>.
/// </summary>
internal sealed class SilkOpenGlOverlayRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _program;
    private readonly int _locTransform;
    private readonly int _locAtlas;
    private readonly int _locEntityAtlas;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly List<OverlayVertex> _skyVertices = new(768);
    private readonly List<OverlayVertex> _worldTriangleVertices = new(8192);
    private readonly List<OverlayVertex> _worldLineVertices = new(128);
    private readonly List<OverlayVertex> _viewModelVertices = new(4096);
    private readonly List<OverlayVertex> _invPreviewVertices = new(512);
    private readonly SilkOpenGlUiRenderer _uiRenderer;
    private bool _disposed;

    public unsafe SilkOpenGlOverlayRenderer(GL gl)
    {
        _gl = gl;
        _program = CreateOverlayShaderProgram(gl);
        _locTransform = gl.GetUniformLocation(_program, "uTransform");
        _locAtlas = gl.GetUniformLocation(_program, "uAtlas");
        _locEntityAtlas = gl.GetUniformLocation(_program, "uEntityAtlas");
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var stride = (uint)Unsafe.SizeOf<OverlayVertex>();
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(7 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.BindVertexArray(0);

        _uiRenderer = new SilkOpenGlUiRenderer(gl);
    }

    public unsafe int RenderOverlays(
        FirstPersonCamera camera,
        FrameOverlayState state,
        uint blockAtlasTexture,
        uint entityAtlasTexture,
        TextureAtlasData? atlasData)
    {
        _skyVertices.Clear();
        _worldTriangleVertices.Clear();
        _worldLineVertices.Clear();
        _viewModelVertices.Clear();

        AppendCelestialSkyAndClouds(_skyVertices, state.WorldTimeTicks, state.CloudDriftOffset, state.IsUnderwater);

        if (state.TargetedBlock is { } target)
        {
            var box = state.TargetedSelectionBox
                ?? new BoundingBox(new Vec3d(target.X, target.Y, target.Z), new Vec3d(target.X + 1.0, target.Y + 1.0, target.Z + 1.0));
            AppendSelectionWireframe(_worldLineVertices, camera.Position, in box);

            if (state.MiningProgress is > 0.01f and < 1.0f)
            {
                AppendBlockDestroyCracks(_worldTriangleVertices, camera.Position, in box, state.MiningProgress);
            }
        }

        AppendRemotePlayerAvatars(_worldTriangleVertices, camera.Position, state.RemotePlayers);
        WorldMob3DMesher.AppendWorldMobs(_worldTriangleVertices, camera.Position, state.WorldMobs);
        WorldMob3DMesher.AppendPrimedTnts(_worldTriangleVertices, camera.Position, state.PrimedTnts);

        if (state.Mode is UiOverlayMode.InGame or UiOverlayMode.ChatInput)
        {
            HeldItem3DMesher.AppendHeldViewmodel(_viewModelVertices, state, atlasData);
        }

        var drawCalls = 0;
        _gl.UseProgram(_program);
        _gl.Uniform1(_locAtlas, 0);
        _gl.Uniform1(_locEntityAtlas, 2);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, blockAtlasTexture);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, entityAtlasTexture);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var viewProj = camera.BuildCameraRelativeViewProjectionMatrix();
        _gl.UniformMatrix4(_locTransform, 1, false, (float*)&viewProj);

        if (_skyVertices.Count > 0)
        {
            _gl.DepthMask(false);
            UploadAndDraw(_skyVertices, PrimitiveType.Triangles);
            _gl.DepthMask(true);
            drawCalls++;
        }

        if (_worldTriangleVertices.Count > 0)
        {
            UploadAndDraw(_worldTriangleVertices, PrimitiveType.Triangles);
            drawCalls++;
        }

        if (_worldLineVertices.Count > 0)
        {
            UploadAndDraw(_worldLineVertices, PrimitiveType.Lines);
            drawCalls++;
        }

        if (_viewModelVertices.Count > 0)
        {
            _gl.Clear(ClearBufferMask.DepthBufferBit);
            var handProj = Matrix4x4.CreatePerspectiveFieldOfView(64.0f * (MathF.PI / 180.0f), camera.AspectRatio, 0.05f, 12.0f);
            _gl.UniformMatrix4(_locTransform, 1, false, (float*)&handProj);
            UploadAndDraw(_viewModelVertices, PrimitiveType.Triangles);
            drawCalls++;
        }

        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.CullFace);
        _gl.BindVertexArray(0);

        drawCalls += _uiRenderer.RenderUi(camera, state, blockAtlasTexture);

        if (state.ShowInventoryPlayerModel && state.InventoryPlayerPreviewRect.Z > 8.0f && state.InventoryPlayerPreviewRect.W > 8.0f)
        {
            drawCalls += RenderInventoryPlayerPreview(state, blockAtlasTexture, entityAtlasTexture);
        }

        return drawCalls;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _uiRenderer.Dispose();
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_program != 0) _gl.DeleteProgram(_program);
        _disposed = true;
    }

    private unsafe int RenderInventoryPlayerPreview(FrameOverlayState state, uint blockAtlasTexture, uint entityAtlasTexture)
    {
        var screenW = state.ViewportSize.X > 64.0f ? state.ViewportSize.X : 1280.0f;
        var screenH = state.ViewportSize.Y > 64.0f ? state.ViewportSize.Y : 720.0f;
        var rect = state.InventoryPlayerPreviewRect;

        var vx = (int)MathF.Round(rect.X);
        var vy = (int)MathF.Round(screenH - (rect.Y + rect.W));
        var vw = Math.Max(8, (int)MathF.Round(rect.Z));
        var vh = Math.Max(8, (int)MathF.Round(rect.W));

        var boxCenterX = rect.X + (rect.Z * 0.5f);
        var boxEyeY = rect.Y + (rect.W * 0.34f);
        var dx = boxCenterX - state.CursorScreenPos.X;
        var dy = boxEyeY - state.CursorScreenPos.Y;
        var bodyYaw = MathF.Atan(dx / 65.0f) * 0.55f;
        var headYaw = MathF.Atan(dx / 50.0f) * 0.65f;
        var headPitch = -MathF.Atan(dy / 55.0f) * 0.55f;

        _invPreviewVertices.Clear();
        var root = Matrix4x4.CreateRotationY(bodyYaw) *
                   Matrix4x4.CreateRotationX(headPitch * 0.18f) *
                   Matrix4x4.CreateTranslation(0.0f, -0.96f, 0.0f);
        WorldMob3DMesher.AppendSteveModel(_invPreviewVertices, root, headYaw - bodyYaw, headPitch, 0.0f, Vector4.One);

        _gl.Viewport(vx, vy, (uint)vw, (uint)vh);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthMask(true);
        _gl.Clear(ClearBufferMask.DepthBufferBit);
        _gl.Disable(EnableCap.CullFace);
        _gl.UseProgram(_program);
        _gl.Uniform1(_locAtlas, 0);
        _gl.Uniform1(_locEntityAtlas, 2);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, blockAtlasTexture);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, entityAtlasTexture);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var aspect = (float)vw / vh;
        var view = Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.06f, 2.85f), new Vector3(0.0f, 0.02f, 0.0f), Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(42.0f * (MathF.PI / 180.0f), aspect, 0.2f, 10.0f);
        var mvp = view * proj;
        _gl.UniformMatrix4(_locTransform, 1, false, (float*)&mvp);
        UploadAndDraw(_invPreviewVertices, PrimitiveType.Triangles);

        _gl.BindVertexArray(0);
        _gl.Enable(EnableCap.CullFace);
        _gl.Viewport(0, 0, (uint)MathF.Round(screenW), (uint)MathF.Round(screenH));
        return 1;
    }

    private static void AppendCelestialSkyAndClouds(List<OverlayVertex> dst, long worldTimeTicks, float driftOffset, bool isUnderwater)
    {
        if (isUnderwater) return;

        var frac = (((worldTimeTicks % 24000L) + 24000L) % 24000L) / 24000.0f;
        var angle = (frac - 0.25f) * MathF.Tau;
        const float radius = 260.0f;
        var sunDir = new Vector3(MathF.Cos(angle), -MathF.Sin(angle), -0.14f);
        sunDir = Vector3.Normalize(sunDir);
        var moonDir = -sunDir;

        // Sun quad (glowing golden-white)
        if (sunDir.Y > -0.25f)
        {
            EmitBillboardQuad(dst, sunDir * radius, 26.0f, new Vector4(1.0f, 0.96f, 0.72f, 0.98f));
            EmitBillboardQuad(dst, sunDir * (radius + 2.0f), 42.0f, new Vector4(1.0f, 0.86f, 0.44f, 0.26f));
        }

        // Moon quad (pale silver-blue)
        if (moonDir.Y > -0.25f)
        {
            EmitBillboardQuad(dst, moonDir * radius, 20.0f, new Vector4(0.86f, 0.92f, 1.0f, 0.94f));
            EmitBillboardQuad(dst, moonDir * (radius - 0.5f), 13.0f, new Vector4(0.68f, 0.76f, 0.90f, 0.55f));
        }

        // Night stars when sun is below horizon
        if (sunDir.Y < 0.08f)
        {
            var starAlpha = Math.Clamp((-sunDir.Y + 0.08f) * 2.5f, 0.0f, 0.88f);
            for (var s = 0; s < 48; s++)
            {
                var h1 = (uint)((s * 747796405) + 2891336453);
                var yaw = ((h1 & 1023) / 1024.0f) * MathF.Tau;
                var elev = 0.14f + ((((h1 >> 10) & 511) / 512.0f) * 1.32f);
                var dir = new Vector3(MathF.Cos(yaw) * MathF.Cos(elev), MathF.Sin(elev), MathF.Sin(yaw) * MathF.Cos(elev));
                EmitBillboardQuad(dst, dir * 275.0f, 1.35f, new Vector4(0.95f, 0.97f, 1.0f, starAlpha));
            }
        }

        var dayCloudFactor = Math.Clamp(0.22f + ((sunDir.Y + 0.25f) * 0.85f), 0.18f, 1.0f);
        var cloudColor = new Vector4(0.97f * dayCloudFactor, 0.98f * dayCloudFactor, 1.0f * dayCloudFactor, 0.74f);
        var wrappedDrift = driftOffset % 192.0f;
        for (var gz = -3; gz <= 3; gz++)
        {
            for (var gx = -3; gx <= 3; gx++)
            {
                var hash = ((gx * 73856093) ^ (gz * 19349663)) & 7;
                if (hash > 2) continue;

                var minX = (gx * 64.0f) + wrappedDrift - 20.0f;
                var maxX = minX + 36.0f + ((hash & 1) * 16.0f);
                var minZ = (gz * 64.0f) - 14.0f;
                var maxZ = minZ + 24.0f + (hash * 8.0f);
                const float y = 112.0f;

                var a = new Vector3(minX, y, minZ);
                var b = new Vector3(maxX, y, minZ);
                var c = new Vector3(maxX, y, maxZ);
                var d = new Vector3(minX, y, maxZ);
                dst.Add(new OverlayVertex(a, cloudColor));
                dst.Add(new OverlayVertex(b, cloudColor));
                dst.Add(new OverlayVertex(c, cloudColor));
                dst.Add(new OverlayVertex(a, cloudColor));
                dst.Add(new OverlayVertex(c, cloudColor));
                dst.Add(new OverlayVertex(d, cloudColor));
            }
        }
    }

    private static void EmitBillboardQuad(List<OverlayVertex> dst, Vector3 center, float halfSize, Vector4 col)
    {
        var right = Vector3.Normalize(Vector3.Cross(center, MathF.Abs(center.Y) > 240.0f ? Vector3.UnitX : Vector3.UnitY)) * halfSize;
        var up = Vector3.Normalize(Vector3.Cross(right, center)) * halfSize;
        var p0 = center - right - up;
        var p1 = center + right - up;
        var p2 = center + right + up;
        var p3 = center - right + up;
        dst.Add(new OverlayVertex(p0, col));
        dst.Add(new OverlayVertex(p1, col));
        dst.Add(new OverlayVertex(p2, col));
        dst.Add(new OverlayVertex(p0, col));
        dst.Add(new OverlayVertex(p2, col));
        dst.Add(new OverlayVertex(p3, col));
    }

    private static void AppendBlockDestroyCracks(List<OverlayVertex> dst, Vec3d cameraPos, in BoundingBox box, float miningProgress)
    {
        var stage = Math.Clamp((int)(miningProgress * 10.0f), 0, 9);
        var tile = ProceduralAtlasGenerator.FirstDestroyStageTile + stage;
        const float eps = 0.0045f;
        var min = new Vector3((float)(box.Min.X - cameraPos.X) - eps, (float)(box.Min.Y - cameraPos.Y) - eps, (float)(box.Min.Z - cameraPos.Z) - eps);
        var max = new Vector3((float)(box.Max.X - cameraPos.X) + eps, (float)(box.Max.Y - cameraPos.Y) + eps, (float)(box.Max.Z - cameraPos.Z) + eps);
        var tint = new Vector4(0.18f, 0.18f, 0.18f, 0.82f);

        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), Matrix4x4.Identity, tile, tint);
    }

    private static void AppendSelectionWireframe(List<OverlayVertex> dst, Vec3d cameraPos, in BoundingBox box)
    {
        const float eps = 0.003f;
        var min = new Vector3((float)(box.Min.X - cameraPos.X) - eps, (float)(box.Min.Y - cameraPos.Y) - eps, (float)(box.Min.Z - cameraPos.Z) - eps);
        var max = new Vector3((float)(box.Max.X - cameraPos.X) + eps, (float)(box.Max.Y - cameraPos.Y) + eps, (float)(box.Max.Z - cameraPos.Z) + eps);
        var col = new Vector4(0.04f, 0.04f, 0.06f, 0.88f);

        Span<Vector3> c =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z), new(min.X, min.Y, max.Z),
            new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
        ];

        ReadOnlySpan<(int A, int B)> edges =
        [
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7),
        ];

        foreach (var (a, b) in edges)
        {
            dst.Add(new OverlayVertex(c[a], col));
            dst.Add(new OverlayVertex(c[b], col));
        }
    }

    private static void AppendRemotePlayerAvatars(List<OverlayVertex> dst, Vec3d cameraPos, IReadOnlyList<RemotePlayerAvatar> avatars)
    {
        for (var i = 0; i < avatars.Count; i++)
        {
            var avatar = avatars[i];
            var relFeet = new Vector3(
                (float)(avatar.FeetPosition.X - cameraPos.X),
                (float)(avatar.FeetPosition.Y - cameraPos.Y),
                (float)(avatar.FeetPosition.Z - cameraPos.Z));
            var yawRad = avatar.YawDegrees * (MathF.PI / 180.0f);
            var pitchRad = avatar.PitchDegrees * (MathF.PI / 180.0f);
            var root = Matrix4x4.CreateRotationY(-yawRad + MathF.PI) * Matrix4x4.CreateTranslation(relFeet);
            WorldMob3DMesher.AppendSteveModel(dst, root, 0.0f, pitchRad, 0.0f, Vector4.One);
        }
    }

    private unsafe void UploadAndDraw(List<OverlayVertex> vertices, PrimitiveType primitiveType)
    {
        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(vertices);
        var byteCount = (nuint)(span.Length * Unsafe.SizeOf<OverlayVertex>());
        fixed (OverlayVertex* ptr = span)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, byteCount, ptr, BufferUsageARB.StreamDraw);
        }

        _gl.DrawArrays(primitiveType, 0, (uint)span.Length);
    }

    private static uint CreateOverlayShaderProgram(GL gl)
    {
        const string vertSrc = """
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec4 aColor;
            layout (location = 2) in vec2 aUv;

            uniform mat4 uTransform;

            out vec4 vColor;
            out vec2 vUv;

            void main()
            {
                vColor = aColor;
                vUv = aUv;
                gl_Position = uTransform * vec4(aPosition, 1.0);
            }
            """;

        const string fragSrc = """
            #version 330 core
            in vec4 vColor;
            in vec2 vUv;

            uniform sampler2D uAtlas;
            uniform sampler2D uEntityAtlas;

            out vec4 FragColor;

            void main()
            {
                if (vUv.x <= -2.0)
                {
                    vec2 entityUv = vec2(-vUv.x - 2.0, vUv.y);
                    vec4 eSample = texture(uEntityAtlas, entityUv);
                    if (eSample.a < 0.1) discard;
                    FragColor = vec4(eSample.rgb * vColor.rgb, eSample.a * vColor.a);
                }
                else if (vUv.x >= 0.0)
                {
                    vec4 tex = texture(uAtlas, vUv);
                    if (tex.a < 0.1) discard;
                    FragColor = vec4(tex.rgb * vColor.rgb, tex.a * vColor.a);
                }
                else
                {
                    FragColor = vColor;
                }
            }
            """;

        var v = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(v, vertSrc);
        gl.CompileShader(v);
        var f = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(f, fragSrc);
        gl.CompileShader(f);
        var prog = gl.CreateProgram();
        gl.AttachShader(prog, v);
        gl.AttachShader(prog, f);
        gl.LinkProgram(prog);
        gl.DetachShader(prog, v);
        gl.DetachShader(prog, f);
        gl.DeleteShader(v);
        gl.DeleteShader(f);
        return prog;
    }
}
