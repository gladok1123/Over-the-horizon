using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Shared.Hashing;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Renders dynamic 3D remote player avatars, targeted block selection highlights,
/// and 2D screen-space crosshair + 9-slot block hotbar overlays in OpenGL 3.3 Core Profile.
/// </summary>
internal sealed class SilkOpenGlOverlayRenderer : IDisposable
{
    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec4 aColor;

        uniform mat4 uTransform;

        out vec4 vColor;

        void main()
        {
            vColor = aColor;
            gl_Position = uTransform * vec4(aPosition, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec4 vColor;
        out vec4 FragColor;

        void main()
        {
            FragColor = vColor;
        }
        """;

    private readonly GL _gl;
    private readonly uint _program;
    private readonly int _locTransform;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly List<OverlayVertex> _scratch = new(1024);
    private bool _disposed;

    public unsafe SilkOpenGlOverlayRenderer(GL gl)
    {
        _gl = gl;
        _program = CompileAndLink(gl);
        _locTransform = gl.GetUniformLocation(_program, "uTransform");

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var stride = (uint)Unsafe.SizeOf<OverlayVertex>();
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
        gl.BindVertexArray(0);
    }

    public unsafe int RenderOverlays(FirstPersonCamera camera, FrameOverlayState overlay)
    {
        var drawCalls = 0;
        _gl.UseProgram(_program);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        // Pass 1: 3D camera-relative remote player avatars + targeted block highlight
        _scratch.Clear();
        foreach (var avatar in overlay.RemotePlayers)
        {
            AppendPlayerAvatarGeometry(camera.Position, in avatar);
        }

        if (_scratch.Count > 0)
        {
            _gl.Enable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);
            _gl.Disable(EnableCap.Blend);
            _gl.DepthMask(true);

            var vp = camera.BuildCameraRelativeViewProjectionMatrix();
            _gl.UniformMatrix4(_locTransform, 1, false, (float*)&vp);
            UploadAndDrawTriangles();
            drawCalls++;
        }

        if (overlay.TargetedBlock is { } blockPos)
        {
            _scratch.Clear();
            var relMin = new Vector3(
                (float)(blockPos.X - camera.Position.X) - 0.004f,
                (float)(blockPos.Y - camera.Position.Y) - 0.004f,
                (float)(blockPos.Z - camera.Position.Z) - 0.004f);
            AppendBoxTriangles(relMin, new Vector3(1.008f, 1.008f, 1.008f), Matrix4x4.Identity, new Vector4(1.0f, 0.95f, 0.55f, 0.22f));

            _gl.Enable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.DepthMask(false);

            var vp = camera.BuildCameraRelativeViewProjectionMatrix();
            _gl.UniformMatrix4(_locTransform, 1, false, (float*)&vp);
            UploadAndDrawTriangles();
            _gl.DepthMask(true);
            drawCalls++;
        }

        // Pass 2: 2D Screen-Space Crosshair + 9-Slot Block Hotbar
        _scratch.Clear();
        AppendHudCrosshairAndHotbar(camera.AspectRatio, overlay.SelectedHotbarSlot, overlay.HotbarSlotColors.Span);

        if (_scratch.Count > 0)
        {
            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            var identity = Matrix4x4.Identity;
            _gl.UniformMatrix4(_locTransform, 1, false, (float*)&identity);
            UploadAndDrawTriangles();
            drawCalls++;
        }

        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.CullFace);
        _gl.Enable(EnableCap.DepthTest);
        _gl.BindVertexArray(0);
        return drawCalls;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

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
        var hash = Fnv1a64.Compute(Encoding.UTF8.GetBytes(username));
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

        // Legs (0.24 x 0.70 x 0.24)
        var leftLegXform = Matrix4x4.CreateRotationX(swing) * Matrix4x4.CreateTranslation(-0.13f, 0.70f, 0.0f) * bodyRot;
        var rightLegXform = Matrix4x4.CreateRotationX(-swing) * Matrix4x4.CreateTranslation(0.13f, 0.70f, 0.0f) * bodyRot;
        AppendBoxTriangles(new Vector3(-0.11f, -0.70f, -0.12f), new Vector3(0.22f, 0.70f, 0.24f), leftLegXform, pants);
        AppendBoxTriangles(new Vector3(-0.11f, -0.70f, -0.12f), new Vector3(0.22f, 0.70f, 0.24f), rightLegXform, pants);

        // Torso (0.48 x 0.70 x 0.26)
        AppendBoxTriangles(new Vector3(-0.24f, 0.70f, -0.13f), new Vector3(0.48f, 0.70f, 0.26f), bodyRot, shirt);

        // Arms (0.20 x 0.68 x 0.22)
        var leftArmXform = Matrix4x4.CreateRotationX(-swing) * Matrix4x4.CreateTranslation(-0.35f, 1.38f, 0.0f) * bodyRot;
        var rightArmXform = Matrix4x4.CreateRotationX(swing) * Matrix4x4.CreateTranslation(0.35f, 1.38f, 0.0f) * bodyRot;
        AppendBoxTriangles(new Vector3(-0.10f, -0.68f, -0.11f), new Vector3(0.20f, 0.68f, 0.22f), leftArmXform, shirt);
        AppendBoxTriangles(new Vector3(-0.10f, -0.68f, -0.11f), new Vector3(0.20f, 0.68f, 0.22f), rightArmXform, shirt);

        // Head (0.46 x 0.46 x 0.46) with pitch look
        var pitchRad = avatar.PitchDegrees * (MathF.PI / 180.0f);
        var headXform = Matrix4x4.CreateRotationX(pitchRad) * Matrix4x4.CreateTranslation(0.0f, 1.40f, 0.0f) * bodyRot;
        AppendBoxTriangles(new Vector3(-0.23f, 0.0f, -0.23f), new Vector3(0.46f, 0.46f, 0.46f), headXform, skin);
    }

    private void AppendHudCrosshairAndHotbar(float aspect, int selectedSlot, ReadOnlySpan<Vector3> slotColors)
    {
        var safeAspect = Math.Max(0.5f, aspect);
        var chHalfW = 0.018f / safeAspect;
        var chThickX = 0.0032f / safeAspect;
        var chHalfH = 0.018f;
        var chThickY = 0.0032f;

        // Crosshair (+)
        AppendQuad2D(-chHalfW, -chThickY, chHalfW * 2.0f, chThickY * 2.0f, new Vector4(0.96f, 0.96f, 0.96f, 0.88f));
        AppendQuad2D(-chThickX, -chHalfH, chThickX * 2.0f, chHalfH * 2.0f, new Vector4(0.96f, 0.96f, 0.96f, 0.88f));

        if (slotColors.IsEmpty)
        {
            return;
        }

        var count = Math.Min(9, slotColors.Length);
        var slotSizeY = 0.095f;
        var slotSizeX = slotSizeY / safeAspect;
        var gapX = 0.012f / safeAspect;
        var totalW = (count * slotSizeX) + ((count - 1) * gapX);
        var startX = -totalW * 0.5f;
        var baseY = -0.94f;

        // Dark backing bar
        AppendQuad2D(startX - gapX, baseY - 0.015f, totalW + (gapX * 2.0f), slotSizeY + 0.03f, new Vector4(0.08f, 0.09f, 0.11f, 0.72f));

        for (var i = 0; i < count; i++)
        {
            var sx = startX + (i * (slotSizeX + gapX));
            if (i == selectedSlot)
            {
                var padX = 0.008f / safeAspect;
                AppendQuad2D(sx - padX, baseY - 0.008f, slotSizeX + (padX * 2.0f), slotSizeY + 0.016f, new Vector4(1.0f, 0.86f, 0.22f, 0.95f));
            }

            var c = slotColors[i];
            var padInnerX = 0.008f / safeAspect;
            AppendQuad2D(sx + padInnerX, baseY + 0.008f, slotSizeX - (padInnerX * 2.0f), slotSizeY - 0.016f, new Vector4(c.X, c.Y, c.Z, 0.95f));
        }
    }

    private void AppendQuad2D(float x, float y, float w, float h, Vector4 color)
    {
        var p0 = new Vector3(x, y, 0.0f);
        var p1 = new Vector3(x + w, y, 0.0f);
        var p2 = new Vector3(x + w, y + h, 0.0f);
        var p3 = new Vector3(x, y + h, 0.0f);

        _scratch.Add(new OverlayVertex(p0, color));
        _scratch.Add(new OverlayVertex(p1, color));
        _scratch.Add(new OverlayVertex(p2, color));
        _scratch.Add(new OverlayVertex(p0, color));
        _scratch.Add(new OverlayVertex(p2, color));
        _scratch.Add(new OverlayVertex(p3, color));
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

        EmitFace(c010, c011, c111, c110, Shade(baseColor, 1.00f)); // Up
        EmitFace(c000, c100, c101, c001, Shade(baseColor, 0.55f)); // Down
        EmitFace(c001, c101, c111, c011, Shade(baseColor, 0.85f)); // South
        EmitFace(c100, c000, c010, c110, Shade(baseColor, 0.82f)); // North
        EmitFace(c101, c100, c110, c111, Shade(baseColor, 0.70f)); // East
        EmitFace(c000, c001, c011, c010, Shade(baseColor, 0.70f)); // West
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
