using Raylib_cs;

namespace OTClient.Framework.Graphics;

/// <summary>
/// Renders dynamic per-tile lighting by compositing a light map (drawn into a
/// <see cref="FrameBuffer"/>) over the game scene using a multiply blend shader.
/// <para>
/// Typical frame sequence:
/// <code>
///   lightView.BeginCapture();
///     // … draw ambient + tile lights …
///   lightView.EndCapture();
///   lightView.Draw();   // composites light map over scene
/// </code>
/// </para>
/// Maps to <c>src/client/lightview.{h,cpp}</c>.
/// </summary>
public sealed class LightView : IDisposable
{
    // ─── Inline GLSL ──────────────────────────────────────────────────────────
    // A minimal Raylib-compatible multiply-blend fragment shader.
    // Multiplies the scene pixel by the light map pixel to produce the lit result.
    private const string MultiplyFragmentGlsl = @"
#version 330 core
in vec2 fragTexCoord;
in vec4 fragColor;
uniform sampler2D texture0;   // scene
uniform sampler2D lightMap;   // light framebuffer texture
out vec4 finalColor;
void main()
{
    vec4 scene = texture(texture0, fragTexCoord) * fragColor;
    vec4 light = texture(lightMap, fragTexCoord);
    finalColor  = scene * light;
}";

    private readonly FrameBuffer _lightBuffer;
    private readonly Shader _shader;
    private readonly int _lightMapLocation;
    private bool _disposed;

    // ─── Constructor ──────────────────────────────────────────────────────────

    /// <param name="width">Viewport width (should match screen width).</param>
    /// <param name="height">Viewport height (should match screen height).</param>
    public LightView(int width, int height)
    {
        _lightBuffer = new FrameBuffer(width, height);
        _shader      = Shader.FromCode(null, MultiplyFragmentGlsl);
        _lightMapLocation = _shader.GetUniformLocation("lightMap");
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>Ambient light colour used to clear the light map each frame.</summary>
    public Color AmbientLight { get; set; } = new Color(40, 40, 40, 255);

    // ─── Capture pass ─────────────────────────────────────────────────────────

    /// <summary>
    /// Redirects subsequent Raylib draw calls into the light map framebuffer.
    /// Call this before drawing ambient and tile lights.
    /// </summary>
    public void BeginCapture() => _lightBuffer.Begin(AmbientLight);

    /// <summary>Ends the light map capture pass.</summary>
    public void EndCapture() => _lightBuffer.End();

    /// <summary>
    /// Draws the light map over the scene using the multiply-blend shader.
    /// Must be called inside <c>BeginDrawing / EndDrawing</c>.
    /// </summary>
    public void Draw()
    {
        // Bind the light map texture to the shader's sampler uniform
        Raylib.SetShaderValueTexture(_shader.Handle, _lightMapLocation, _lightBuffer.Texture);

        _shader.Begin();
        _lightBuffer.DrawToScreen(0, 0);
        _shader.End();
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lightBuffer.Dispose();
        _shader.Dispose();
    }
}
