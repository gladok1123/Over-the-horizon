using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using OverHorizon.Shared.Hashing;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Renders 3D sky sun &amp; clouds, 3D friend avatars, 3D peaceful &amp; hostile mobs, targeted block wireframe,
/// world-lit textured 3D held blocks &amp; pixel-extruded 3D tools/items, and delegates 2D GUI to <see cref="SilkOpenGlUiRenderer"/>.
/// </summary>
internal sealed class SilkOpenGlOverlayRenderer : IDisposable
{
    private const string VertexShaderSource = """
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

    private const string FragmentShaderSource = """
        #version 330 core
        in vec4 vColor;
        in vec2 vUv;

        uniform sampler2D uAtlas;

        out vec4 FragColor;

        void main()
        {
            if (vUv.x >= -0.25)
            {
                vec4 texel = texture(uAtlas, vUv);
                if (texel.a < 0.08)
                {
                    discard;
                }

                FragColor = texel * vColor;
                return;
            }

            FragColor = vColor;
        }
        """;

    private readonly GL _gl;
    private readonly uint _program;
    private readonly int _locTransform;
    private readonly int _locAtlas;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly SilkOpenGlUiRenderer _uiRenderer;
    private readonly List<OverlayVertex> _scratch = new(4096);
    private bool _disposed;

    public unsafe SilkOpenGlOverlayRenderer(GL gl, string? externalAssetsPath = null)
    {
        _gl = gl;
        _program = CompileAndLink(gl);
        _locTransform = gl.GetUniformLocation(_program, "uTransform");
        _locAtlas = gl.GetUniformLocation(_program, "uAtlas");
        _uiRenderer = new SilkOpenGlUiRenderer(gl, externalAssetsPath);

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
    }

    public unsafe int RenderOverlays(
        FirstPersonCamera camera,
        FrameOverlayState overlay,
        uint itemAtlasTexture = 0,
        TextureAtlasData? atlasData = null)
    {
        var drawCalls = 0;
        var vp = camera.BuildCameraRelativeViewProjectionMatrix();

        _gl.UseProgram(_program);
        _gl.Uniform1(_locAtlas, 0);
        if (itemAtlasTexture != 0)
        {
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, itemAtlasTexture);
        }

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        // Pass 1: 3D Sky Sun + Drifting Voxel Clouds + Remote Friend Avatars + 3D World Mobs
        _scratch.Clear();
        if (!overlay.IsUnderwater)
        {
            AppendSkySunAndClouds(overlay.CloudDriftOffset);
        }

        foreach (var avatar in overlay.RemotePlayers)
        {
            AppendPlayerAvatarGeometry(camera.Position, in avatar);
        }

        WorldMob3DMesher.AppendWorldMobs(_scratch, camera.Position, overlay.WorldMobs);

        if (_scratch.Count > 0)
        {
            _gl.Enable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);
            _gl.Disable(EnableCap.Blend);
            _gl.DepthMask(true);

            _gl.UniformMatrix4(_locTransform, 1, false, (float*)&vp);
            UploadAndDrawTriangles();
            drawCalls++;
        }

        // Pass 2: Targeted Block Selection Highlight Box
        if (overlay.Mode == UiOverlayMode.InGame && overlay.TargetedBlock is { } blockPos)
        {
            _scratch.Clear();
            var relMin = new Vector3(
                (float)(blockPos.X - camera.Position.X) - 0.005f,
                (float)(blockPos.Y - camera.Position.Y) - 0.005f,
                (float)(blockPos.Z - camera.Position.Z) - 0.005f);
            AppendBoxTriangles(relMin, new Vector3(1.010f, 1.010f, 1.010f), Matrix4x4.Identity, new Vector4(0.08f, 0.08f, 0.10f, 0.18f));
            AppendWireframeEdges(relMin, new Vector3(1.012f, 1.012f, 1.012f), 0.011f, new Vector4(0.04f, 0.04f, 0.05f, 0.86f));

            _gl.Enable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.DepthMask(false);

            _gl.UniformMatrix4(_locTransform, 1, false, (float*)&vp);
            UploadAndDrawTriangles();
            _gl.DepthMask(true);
            drawCalls++;
        }

        // Pass 3: Authentic Minecraft 3D Held Block / Torch / 16x16 Pixel-Extruded Sword & Tool Viewmodel (lit by 0..15 world light!)
        if (overlay.Mode is not (UiOverlayMode.TitleMenu or UiOverlayMode.DeathScreen))
        {
            _scratch.Clear();
            HeldItem3DMesher.AppendHeldViewmodel(_scratch, overlay, atlasData);

            if (_scratch.Count > 0)
            {
                _gl.Clear((uint)ClearBufferMask.DepthBufferBit);
                _gl.Enable(EnableCap.DepthTest);
                _gl.Disable(EnableCap.CullFace);
                _gl.Disable(EnableCap.Blend);

                var viewProjHand = Matrix4x4.CreatePerspectiveFieldOfView(
                    64.0f * (MathF.PI / 180.0f),
                    Math.Max(0.5f, camera.AspectRatio),
                    0.05f,
                    16.0f);
                _gl.UniformMatrix4(_locTransform, 1, false, (float*)&viewProjHand);
                UploadAndDrawTriangles();
                drawCalls++;
            }
        }

        _gl.BindVertexArray(0);

        // Pass 4: 2D Screen-Space Real Game GUI (widgets.png, icons.png, container/*.png), Sprites, & Text
        drawCalls += _uiRenderer.RenderUi(camera, overlay, itemAtlasTexture);
        return drawCalls;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _uiRenderer.Dispose();
        if (_vao != 0)
        {
            _gl.DeleteVertexArray(_vao);
        }

        if (_vbo != 0)
        {
            _gl.DeleteBuffer(_vbo);
        }

        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
        }

        _disposed = true;
    }

    private static uint CompileAndLink(GL gl)
    {
        var vs = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vs, VertexShaderSource);
        gl.CompileShader(vs);

        var fs = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fs, FragmentShaderSource);
        gl.CompileShader(fs);

        var prog = gl.CreateProgram();
        gl.AttachShader(prog, vs);
        gl.AttachShader(prog, fs);
        gl.LinkProgram(prog);
        gl.DetachShader(prog, vs);
        gl.DetachShader(prog, fs);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return prog;
    }

    private static (Vector4 Shirt, Vector4 Pants) ComputeOutfitColors(string username)
    {
        var hasher = Fnv1a64.Create();
        hasher.AppendString(username);
        var hash = hasher.Value;
        var r = 0.25f + (((hash & 0xFFUL) / 255.0f) * 0.65f);
        var g = 0.25f + ((((hash >> 8) & 0xFFUL) / 255.0f) * 0.65f);
        var b = 0.25f + ((((hash >> 16) & 0xFFUL) / 255.0f) * 0.65f);
        return (new Vector4(r, g, b, 1.0f), new Vector4(r * 0.45f, g * 0.45f, b * 0.55f, 1.0f));
    }

    private static Vector4 Shade(Vector4 c, float factor) =>
        new(c.X * factor, c.Y * factor, c.Z * factor, c.W);

    private unsafe void UploadAndDrawTriangles()
    {
        var span = CollectionsMarshal.AsSpan(_scratch);
        fixed (OverlayVertex* ptr = span)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(span.Length * Unsafe.SizeOf<OverlayVertex>()),
                ptr,
                BufferUsageARB.StreamDraw);
        }

        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)span.Length);
    }

    private void AppendSkySunAndClouds(float drift)
    {
        AppendBoxTriangles(new Vector3(48.0f, 118.0f, -95.0f), new Vector3(16.0f, 16.0f, 16.0f), Matrix4x4.Identity, new Vector4(1.0f, 0.96f, 0.68f, 1.0f));
        AppendBoxTriangles(new Vector3(50.0f, 120.0f, -93.0f), new Vector3(12.0f, 12.0f, 12.0f), Matrix4x4.Identity, new Vector4(1.0f, 1.0f, 0.90f, 1.0f));

        var wrapped = (drift % 160.0f) - 80.0f;
        var cloudCol = new Vector4(0.97f, 0.98f, 1.0f, 1.0f);
        for (var i = -3; i <= 3; i++)
        {
            var cx = wrapped + (i * 42.0f);
            var cz = ((i * 29) % 70) - 35.0f;
            AppendBoxTriangles(new Vector3(cx, 88.0f, cz), new Vector3(22.0f, 3.2f, 14.0f), Matrix4x4.Identity, cloudCol);
            AppendBoxTriangles(new Vector3(cx + 6.0f, 88.0f, cz - 8.0f), new Vector3(14.0f, 2.8f, 10.0f), Matrix4x4.Identity, cloudCol);
        }
    }

    private void AppendWireframeEdges(Vector3 min, Vector3 size, float t, Vector4 color)
    {
        var max = min + size;
        AppendBoxTriangles(new Vector3(min.X, min.Y, min.Z), new Vector3(size.X, t, t), Matrix4x4.Identity, color);
        AppendBoxTriangles(new Vector3(min.X, min.Y, max.Z - t), new Vector3(size.X, t, t), Matrix4x4.Identity, color);
        AppendBoxTriangles(new Vector3(min.X, max.Y - t, min.Z), new Vector3(size.X, t, t), Matrix4x4.Identity, color);
        AppendBoxTriangles(new Vector3(min.X, max.Y - t, max.Z - t), new Vector3(size.X, t, t), Matrix4x4.Identity, color);

        AppendBoxTriangles(new Vector3(min.X, min.Y, min.Z), new Vector3(t, t, size.Z), Matrix4x4.Identity, color);
        AppendBoxTriangles(new Vector3(max.X - t, min.Y, min.Z), new Vector3(t, t, size.Z), Matrix4x4.Identity, color);
        AppendBoxTriangles(new Vector3(min.X, max.Y - t, min.Z), new Vector3(t, t, size.Z), Matrix4x4.Identity, color);
        AppendBoxTriangles(new Vector3(max.X - t, max.Y - t, min.Z), new Vector3(t, t, size.Z), Matrix4x4.Identity, color);

        AppendBoxTriangles(new Vector3(min.X, min.Y, min.Z), new Vector3(t, size.Y, t), Matrix4x4.Identity, color);
        AppendBoxTriangles(new Vector3(max.X - t, min.Y, min.Z), new Vector3(t, size.Y, t), Matrix4x4.Identity, color);
        AppendBoxTriangles(new Vector3(min.X, min.Y, max.Z - t), new Vector3(t, size.Y, t), Matrix4x4.Identity, color);
        AppendBoxTriangles(new Vector3(max.X - t, min.Y, max.Z - t), new Vector3(t, size.Y, t), Matrix4x4.Identity, color);
    }

    private void AppendPlayerAvatarGeometry(Vec3d cameraPos, in RemotePlayerAvatar avatar)
    {
        var baseOffset = new Vector3(
            (float)(avatar.FeetPosition.X - cameraPos.X),
            (float)(avatar.FeetPosition.Y - cameraPos.Y),
            (float)(avatar.FeetPosition.Z - cameraPos.Z));

        var bodyYawRad = -avatar.YawDegrees * (MathF.PI / 180.0f);
        var bodyRot = Matrix4x4.CreateRotationY(bodyYawRad) * Matrix4x4.CreateTranslation(baseOffset);
        var swing = MathF.Sin(avatar.WalkPhase) * 0.45f;

        var (shirt, pants) = ComputeOutfitColors(avatar.Username);
        var skin = new Vector4(0.92f, 0.74f, 0.60f, 1.0f);
        var hair = new Vector4(0.26f, 0.18f, 0.11f, 1.0f);
        if (avatar.HurtFlash > 0.01f)
        {
            var hurtTint = new Vector4(1.0f, 0.22f, 0.20f, 1.0f);
            var f = Math.Clamp(avatar.HurtFlash, 0.0f, 1.0f) * 0.78f;
            shirt = Vector4.Lerp(shirt, hurtTint, f);
            pants = Vector4.Lerp(pants, hurtTint, f);
            skin = Vector4.Lerp(skin, hurtTint, f);
            hair = Vector4.Lerp(hair, hurtTint, f);
        }

        var eyeCol = new Vector4(0.14f, 0.22f, 0.48f, 1.0f);

        var leftLegXform = Matrix4x4.CreateRotationX(swing) * Matrix4x4.CreateTranslation(-0.13f, 0.70f, 0.0f) * bodyRot;
        var rightLegXform = Matrix4x4.CreateRotationX(-swing) * Matrix4x4.CreateTranslation(0.13f, 0.70f, 0.0f) * bodyRot;
        AppendBoxTriangles(new Vector3(-0.11f, -0.70f, -0.12f), new Vector3(0.22f, 0.70f, 0.24f), leftLegXform, pants);
        AppendBoxTriangles(new Vector3(-0.11f, -0.70f, -0.12f), new Vector3(0.22f, 0.70f, 0.24f), rightLegXform, pants);

        AppendBoxTriangles(new Vector3(-0.24f, 0.70f, -0.13f), new Vector3(0.48f, 0.70f, 0.26f), bodyRot, shirt);

        var leftArmXform = Matrix4x4.CreateRotationX(-swing) * Matrix4x4.CreateTranslation(-0.35f, 1.38f, 0.0f) * bodyRot;
        var rightArmXform = Matrix4x4.CreateRotationX(swing) * Matrix4x4.CreateTranslation(0.35f, 1.38f, 0.0f) * bodyRot;
        AppendBoxTriangles(new Vector3(-0.10f, -0.68f, -0.11f), new Vector3(0.20f, 0.68f, 0.22f), leftArmXform, shirt);
        AppendBoxTriangles(new Vector3(-0.10f, -0.68f, -0.11f), new Vector3(0.20f, 0.68f, 0.22f), rightArmXform, shirt);

        var pitchRad = avatar.PitchDegrees * (MathF.PI / 180.0f);
        var headXform = Matrix4x4.CreateRotationX(pitchRad) * Matrix4x4.CreateTranslation(0.0f, 1.40f, 0.0f) * bodyRot;
        AppendBoxTriangles(new Vector3(-0.23f, 0.0f, -0.23f), new Vector3(0.46f, 0.46f, 0.46f), headXform, skin);
        AppendBoxTriangles(new Vector3(-0.24f, 0.34f, -0.24f), new Vector3(0.48f, 0.14f, 0.48f), headXform, hair);
        AppendBoxTriangles(new Vector3(-0.14f, 0.18f, -0.245f), new Vector3(0.08f, 0.07f, 0.02f), headXform, eyeCol);
        AppendBoxTriangles(new Vector3(0.06f, 0.18f, -0.245f), new Vector3(0.08f, 0.07f, 0.02f), headXform, eyeCol);
    }

    private void AppendBoxTriangles(Vector3 min, Vector3 size, Matrix4x4 transform, Vector4 baseColor)
    {
        var max = min + size;
        var c000 = Vector3.Transform(new Vector3(min.X, min.Y, min.Z), transform);
        var c100 = Vector3.Transform(new Vector3(max.X, min.Y, min.Z), transform);
        var c010 = Vector3.Transform(new Vector3(min.X, max.Y, min.Z), transform);
        var c110 = Vector3.Transform(new Vector3(max.X, max.Y, min.Z), transform);
        var c001 = Vector3.Transform(new Vector3(min.X, min.Y, max.Z), transform);
        var c101 = Vector3.Transform(new Vector3(max.X, min.Y, max.Z), transform);
        var c011 = Vector3.Transform(new Vector3(min.X, max.Y, max.Z), transform);
        var c111 = Vector3.Transform(new Vector3(max.X, max.Y, max.Z), transform);

        EmitFace(c010, c011, c111, c110, Shade(baseColor, 1.00f));
        EmitFace(c000, c100, c101, c001, Shade(baseColor, 0.55f));
        EmitFace(c001, c101, c111, c011, Shade(baseColor, 0.85f));
        EmitFace(c100, c000, c010, c110, Shade(baseColor, 0.82f));
        EmitFace(c101, c100, c110, c111, Shade(baseColor, 0.70f));
        EmitFace(c000, c001, c011, c010, Shade(baseColor, 0.70f));
    }

    private void EmitFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 color)
    {
        _scratch.Add(new OverlayVertex(a, color));
        _scratch.Add(new OverlayVertex(b, color));
        _scratch.Add(new OverlayVertex(c, color));
        _scratch.Add(new OverlayVertex(a, color));
        _scratch.Add(new OverlayVertex(c, color));
        _scratch.Add(new OverlayVertex(d, color));
    }
}
