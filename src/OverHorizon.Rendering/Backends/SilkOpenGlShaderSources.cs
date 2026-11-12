namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Embedded GLSL 330 core vertex and fragment shaders for greedy-meshed voxel terrain rendering
/// with camera-relative anti-jitter coordinates, tile-local UV wrapping, smooth AO, underwater absorption, and fog.
/// </summary>
public static class SilkOpenGlShaderSources
{
    /// <summary>GLSL 330 core chunk vertex shader.</summary>
    public const string ChunkVertexShader = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec2 aUv;
        layout (location = 2) in float aTileIndex;
        layout (location = 3) in float aShadeAndAo;
        layout (location = 4) in float aLightLevel;

        uniform mat4 uViewProjection;
        uniform vec3 uSectionOffset;

        out vec2 vUv;
        flat out float vTileIndex;
        out float vShadeAndAo;
        out float vLightLevel;
        out float vDistance;

        void main()
        {
            vec3 cameraRelativePos = uSectionOffset + aPosition;
            vUv = aUv;
            vTileIndex = aTileIndex;
            vShadeAndAo = aShadeAndAo;
            vLightLevel = aLightLevel;
            vDistance = length(cameraRelativePos);
            gl_Position = uViewProjection * vec4(cameraRelativePos, 1.0);
        }
        """;

    /// <summary>GLSL 330 core chunk fragment shader with authentic underwater light absorption and fog.</summary>
    public const string ChunkFragmentShader = """
        #version 330 core
        in vec2 vUv;
        flat in float vTileIndex;
        in float vShadeAndAo;
        in float vLightLevel;
        in float vDistance;

        uniform sampler2D uAtlas;
        uniform float uSkyBrightness;
        uniform float uFogStart;
        uniform float uFogEnd;
        uniform vec3 uSkyColor;
        uniform float uUnderwater;

        out vec4 FragColor;

        void main()
        {
            int tileIdx = int(vTileIndex + 0.5);
            int tileCol = tileIdx & 15;
            int tileRow = tileIdx >> 4;

            vec2 localUv = clamp(fract(vUv), vec2(0.002), vec2(0.998));
            vec2 atlasUv = (vec2(float(tileCol), float(tileRow)) + localUv) / 16.0;

            vec4 texel = texture(uAtlas, atlasUv);
            if (texel.a < 0.05)
            {
                discard;
            }

            float minAmbient = mix(0.06, 0.22, uUnderwater);
            float illumination = clamp(vLightLevel * uSkyBrightness, minAmbient, 1.0) * vShadeAndAo;
            vec3 litColor = texel.rgb * illumination;
            litColor = mix(litColor, litColor * vec3(0.54, 0.84, 1.05), uUnderwater * 0.72);

            float fogFactor = clamp((vDistance - uFogStart) / max(1.0, uFogEnd - uFogStart), 0.0, 1.0);
            vec3 finalRgb = mix(litColor, uSkyColor, fogFactor);
            FragColor = vec4(finalRgb, texel.a);
        }
        """;
}
