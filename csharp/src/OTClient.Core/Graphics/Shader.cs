using Raylib_cs;

namespace OTClient.Framework.Graphics;

/// <summary>
/// Wraps a Raylib <see cref="Raylib_cs.Shader"/> and provides a typed API for
/// loading from GLSL source files or inline strings and setting uniform values.
/// <para>
/// Dispose to release GPU memory.
/// </para>
/// Maps to <c>src/framework/graphics/shader.{h,cpp}</c> and
/// <c>paintershaderprogram.{h,cpp}</c>.
/// </summary>
public sealed class Shader : IDisposable
{
    private Raylib_cs.Shader _handle;
    private bool _disposed;

    // ─── Factories ────────────────────────────────────────────────────────────

    private Shader(Raylib_cs.Shader handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Loads a shader from GLSL source files.
    /// Pass <c>null</c> for <paramref name="vertexPath"/> to use the default
    /// Raylib vertex shader.
    /// </summary>
    public static Shader FromFiles(string? vertexPath, string fragmentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentPath);
        var handle = Raylib.LoadShader(vertexPath, fragmentPath);
        return new Shader(handle);
    }

    /// <summary>
    /// Creates a shader from inline GLSL source strings.
    /// Pass <c>null</c> for <paramref name="vertexSource"/> to use the default
    /// Raylib vertex shader.
    /// </summary>
    public static Shader FromCode(string? vertexSource, string fragmentSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentSource);
        var handle = Raylib.LoadShaderFromMemory(vertexSource, fragmentSource);
        return new Shader(handle);
    }

    // ─── Properties ───────────────────────────────────────────────────────────

    /// <summary>Underlying Raylib handle.</summary>
    public Raylib_cs.Shader Handle => _handle;

    // ─── Uniforms ─────────────────────────────────────────────────────────────

    /// <summary>Returns the location of a named uniform, or -1 if not found.</summary>
    public int GetUniformLocation(string name) =>
        Raylib.GetShaderLocation(_handle, name);

    /// <summary>Sets a float uniform.</summary>
    public void SetUniform(int location, float value)
    {
        Raylib.SetShaderValue(_handle, location, value, ShaderUniformDataType.Float);
    }

    /// <summary>Sets an int uniform.</summary>
    public void SetUniform(int location, int value)
    {
        Raylib.SetShaderValue(_handle, location, value, ShaderUniformDataType.Int);
    }

    /// <summary>Sets a vec2 uniform from a <see cref="Vector2"/>.</summary>
    public void SetUniform(int location, Vector2 value)
    {
        Raylib.SetShaderValue(_handle, location, value, ShaderUniformDataType.Vec2);
    }

    /// <summary>Sets a vec4 (colour) uniform from a <see cref="Vector4"/>.</summary>
    public void SetUniform(int location, Vector4 value)
    {
        Raylib.SetShaderValue(_handle, location, value, ShaderUniformDataType.Vec4);
    }

    // ─── Begin / End ──────────────────────────────────────────────────────────

    /// <summary>
    /// Activates this shader for subsequent Raylib draw calls.
    /// Must be paired with <see cref="End"/>.
    /// </summary>
    public void Begin() => Raylib.BeginShaderMode(_handle);

    /// <summary>Deactivates the shader and restores the default Raylib shader.</summary>
    public void End() => Raylib.EndShaderMode();

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle.Id != 0)
        {
            Raylib.UnloadShader(_handle);
            _handle = default;
        }
    }
}
