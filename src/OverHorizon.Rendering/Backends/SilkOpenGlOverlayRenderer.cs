using System.Numerics;
using System.Runtime.CompilerServices;
using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Renders the pre-terrain Celestial Sky Dome (Sun, Moon, Stars), world-anchored drifting Clouds,
/// sub-voxel selection wireframes, <c>destroy_stage_0..9</c> crack overlays, articulated 3D mobs,
/// 3D Minecarts, 3D Arrows, Remote Players with 3D Armor, first-person viewmodels, and the 3D Inventory Steve preview.
/// </summary>
internal sealed partial class SilkOpenGlOverlayRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _program;
    private readonly int _locTransform;
    private readonly int _locAtlas;
    private readonly int _locEntityAtlas;
    private readonly int _locFogStart;
    private readonly int _locFogEnd;
    private readonly int _locFogColor;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly List<OverlayVertex> _skyVertices = new(768);
    private readonly List<OverlayVertex> _cloudVertices = new(512);
    private readonly List<OverlayVertex> _worldTriangleVertices = new(12288);
    private readonly List<OverlayVertex> _worldLineVertices = new(128);
    private readonly List<OverlayVertex> _viewModelVertices = new(4096);
    private readonly List<OverlayVertex> _invPreviewVertices = new(1024);
    private readonly SilkOpenGlUiRenderer _uiRenderer;
    private bool _disposed;

    public unsafe SilkOpenGlOverlayRenderer(GL gl)
    {
        _gl = gl;
        _program = CreateOverlayShaderProgram(gl);
        _locTransform = gl.GetUniformLocation(_program, "uTransform");
        _locAtlas = gl.GetUniformLocation(_program, "uAtlas");
        _locEntityAtlas = gl.GetUniformLocation(_program, "uEntityAtlas");
        _locFogStart = gl.GetUniformLocation(_program, "uFogStart");
        _locFogEnd = gl.GetUniformLocation(_program, "uFogEnd");
        _locFogColor = gl.GetUniformLocation(_program, "uFogColor");
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

    /// <summary>Draws Sun, Moon, and Stars behind all terrain with DepthTest &amp; DepthMask disabled so FarPlane never clips them.</summary>
    public unsafe int RenderSkyDome(FirstPersonCamera camera, FrameOverlayState state)
    {
        if (state.IsUnderwater) return 0;
        if (camera.Position.Y < OverHorizon.Core.EngineConstants.WorldMinY + 1.0 ||
            (camera.Position.Y < 44.0 && state.CameraSkyLightNormalized < 0.05f))
        {
            return 0;
        }

        _skyVertices.Clear();
        AppendCelestialBodies(_skyVertices, state.WorldTimeTicks, camera.Position.Y);
        if (_skyVertices.Count == 0) return 0;

        _gl.UseProgram(_program);
        SetFogUniforms(100000.0f, 200000.0f, Vector3.One);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var skyProj = Matrix4x4.CreatePerspectiveFieldOfView(
            camera.FieldOfViewDegrees * (MathF.PI / 180.0f),
            camera.AspectRatio,
            0.5f,
            1024.0f);
        var skyMvp = camera.ComputeRelativeViewMatrix() * skyProj;
        _gl.UniformMatrix4(_locTransform, 1, false, (float*)&skyMvp);
        UploadAndDraw(_skyVertices, PrimitiveType.Triangles);

        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        return 1;
    }

    public unsafe int RenderOverlays(
        FirstPersonCamera camera,
        FrameOverlayState state,
        uint blockAtlasTexture,
        uint entityAtlasTexture,
        TextureAtlasData? atlasData,
        Vector3 fogColor,
        float chunkFogDist)
    {
        _cloudVertices.Clear();
        _worldTriangleVertices.Clear();
        _worldLineVertices.Clear();
        _viewModelVertices.Clear();

        if (!state.IsUnderwater && camera.Position.Y > OverHorizon.Core.EngineConstants.WorldMinY - 2.0)
        {
            AppendWorldClouds(_cloudVertices, camera.Position, state.WorldTimeTicks, state.CloudDriftOffset, state.CameraSkyLightNormalized);
        }

        if (camera.Position.Y < 24.0)
        {
            AppendVoidAbyssFloor(_worldTriangleVertices, camera.Position);
        }

        if (state.TargetedBlock is { } target)
        {
            var box = state.TargetedSelectionBox ?? BoundingBox.FromBlock(target);
            AppendSelectionWireframe(_worldLineVertices, camera.Position, in box);

            if (state.MiningProgress is > 0.01f and < 1.0f)
            {
                AppendBlockDestroyCracks(_worldTriangleVertices, camera.Position, in box, state.MiningProgress);
            }
        }

        WorldMob3DMesher.AppendRemotePlayers(_worldTriangleVertices, camera.Position, state.RemotePlayers);
        if (state.IsThirdPersonCamera && state.LocalThirdPersonPlayer is { } localThirdPerson)
        {
            WorldMob3DMesher.AppendSingleRemotePlayer(_worldTriangleVertices, camera.Position, localThirdPerson);
        }

        WorldMob3DMesher.AppendWorldMobs(_worldTriangleVertices, camera.Position, state.WorldMobs, atlasData);
        WorldMob3DMesher.AppendMinecarts(_worldTriangleVertices, camera.Position, state.Minecarts);
        WorldMob3DMesher.AppendArrows(_worldTriangleVertices, camera.Position, state.Arrows);
        WorldMob3DMesher.AppendPrimedTnts(_worldTriangleVertices, camera.Position, state.PrimedTnts);
        WorldMob3DMesher.AppendFallingBlocks(_worldTriangleVertices, camera.Position, state.FallingBlocks);
        WorldMob3DMesher.AppendDroppedItems(_worldTriangleVertices, camera.Position, state.DroppedItems, atlasData);
        WorldMob3DMesher.AppendWorldParticles(_worldTriangleVertices, camera, state.Particles);

        if (!state.IsThirdPersonCamera && state.Mode is UiOverlayMode.InGame or UiOverlayMode.ChatInput)
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

        if (_cloudVertices.Count > 0)
        {
            SetFogUniforms(180.0f, 360.0f, fogColor);
            _gl.DepthMask(false);
            UploadAndDraw(_cloudVertices, PrimitiveType.Triangles);
            _gl.DepthMask(true);
            drawCalls++;
        }

        var entFogStart = state.IsUnderwater ? 0.6f : chunkFogDist * 0.62f;
        var entFogEnd = state.IsUnderwater ? 11.5f : chunkFogDist * 0.98f;
        SetFogUniforms(entFogStart, entFogEnd, fogColor);

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

        SetFogUniforms(100000.0f, 200000.0f, fogColor);

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

    private void SetFogUniforms(float start, float end, Vector3 color)
    {
        _gl.Uniform1(_locFogStart, start);
        _gl.Uniform1(_locFogEnd, end);
        _gl.Uniform3(_locFogColor, color.X, color.Y, color.Z);
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
        var dx = state.CursorScreenPos.X - boxCenterX;
        var dy = state.CursorScreenPos.Y - boxEyeY;
        var bodyYaw = MathF.Atan(dx / 65.0f) * 0.55f;
        var headYaw = MathF.Atan(dx / 50.0f) * 0.68f;
        var headPitch = MathF.Atan(dy / 55.0f) * 0.55f;

        _invPreviewVertices.Clear();
        var root = Matrix4x4.CreateRotationY(bodyYaw) *
                   Matrix4x4.CreateRotationX(headPitch * 0.18f) *
                   Matrix4x4.CreateTranslation(0.0f, -0.96f, 0.0f);
        WorldMob3DMesher.AppendSteveModel(_invPreviewVertices, root, headYaw - bodyYaw, headPitch, 0.0f, Vector4.One, state.LocalPlayerArmor);

        _gl.Viewport(vx, vy, (uint)vw, (uint)vh);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthMask(true);
        _gl.Clear(ClearBufferMask.DepthBufferBit);
        _gl.Disable(EnableCap.CullFace);
        _gl.UseProgram(_program);
        SetFogUniforms(100000.0f, 200000.0f, Vector3.One);
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

    private static void AppendBlockDestroyCracks(List<OverlayVertex> dst, Vec3d cameraPos, in BoundingBox box, float miningProgress)
    {
        var stage = Math.Clamp((int)(miningProgress * 10.0f), 0, 9);
        var tile = ProceduralAtlasGenerator.FirstDestroyStageTile + stage;
        const float eps = 0.0045f;
        var min = new Vector3((float)(box.MinX - cameraPos.X) - eps, (float)(box.MinY - cameraPos.Y) - eps, (float)(box.MinZ - cameraPos.Z) - eps);
        var max = new Vector3((float)(box.MaxX - cameraPos.X) + eps, (float)(box.MaxY - cameraPos.Y) + eps, (float)(box.MaxZ - cameraPos.Z) + eps);
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
        var min = new Vector3((float)(box.MinX - cameraPos.X) - eps, (float)(box.MinY - cameraPos.Y) - eps, (float)(box.MinZ - cameraPos.Z) - eps);
        var max = new Vector3((float)(box.MaxX - cameraPos.X) + eps, (float)(box.MaxY - cameraPos.Y) + eps, (float)(box.MaxZ - cameraPos.Z) + eps);
        var col = new Vector4(0.04f, 0.04f, 0.06f, 0.88f);

        Span<Vector3> c =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z), new(min.X, min.Y, max.Z),
            new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
        ];

        ReadOnlySpan<(int A, int B)> edges = [(0, 1), (1, 2), (2, 3), (3, 0), (4, 5), (5, 6), (6, 7), (7, 4), (0, 4), (1, 5), (2, 6), (3, 7)];
        foreach (var (a, b) in edges)
        {
            dst.Add(new OverlayVertex(c[a], col));
            dst.Add(new OverlayVertex(c[b], col));
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
            out float vDist;

            void main()
            {
                vColor = aColor;
                vUv = aUv;
                vDist = length(aPosition);
                gl_Position = uTransform * vec4(aPosition, 1.0);
            }
            """;

        const string fragSrc = """
            #version 330 core
            in vec4 vColor;
            in vec2 vUv;
            in float vDist;

            uniform sampler2D uAtlas;
            uniform sampler2D uEntityAtlas;
            uniform float uFogStart;
            uniform float uFogEnd;
            uniform vec3 uFogColor;

            out vec4 FragColor;

            void main()
            {
                vec4 col;
                if (vUv.x <= -2.0)
                {
                    vec2 entityUv = vec2(-vUv.x - 2.0, vUv.y);
                    vec4 eSample = texture(uEntityAtlas, entityUv);
                    if (eSample.a < 0.1) discard;
                    col = vec4(eSample.rgb * vColor.rgb, eSample.a * vColor.a);
                }
                else if (vUv.x >= 0.0)
                {
                    vec4 tex = texture(uAtlas, vUv);
                    if (tex.a < 0.1) discard;
                    col = vec4(tex.rgb * vColor.rgb, tex.a * vColor.a);
                }
                else
                {
                    col = vColor;
                }

                float fog = clamp((vDist - uFogStart) / max(0.01, uFogEnd - uFogStart), 0.0, 1.0);
                FragColor = vec4(mix(col.rgb, uFogColor, fog), col.a);
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
