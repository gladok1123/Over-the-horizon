namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Embedded GLSL 330 core vertex and fragment shaders for greedy-meshed voxel terrain rendering
/// with separate <c>0..15</c> sky &amp; block illumination, dynamic held-torch <c>0..15</c> lighting,
/// smooth AO, underwater absorption, and horizon fog.
/// </summary>
public static class SilkOpenGlShaderSources
{
    /// <summary>GLSL 330 core chunk vertex shader decoding <c>0..15</c> sky and block light nibbles.</summary>
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
        out float vSkyLight;
        out float vBlockLight;
        out vec3 vCamRelPos;
        out float vDistance;

        void main()
        {
            vec3 cameraRelativePos = uSectionOffset + aPosition;
            int packedLight = int(aLightLevel + 0.5);
            vUv = aUv;
            vTileIndex = aTileIndex;
            vShadeAndAo = aShadeAndAo;
            vSkyLight = float((packedLight >> 4) & 15);
            vBlockLight = float(packedLight & 15);
            vCamRelPos = cameraRelativePos;
            vDistance = length(cameraRelativePos);
            gl_Position = uViewProjection * vec4(cameraRelativePos, 1.0);
        }
        """;

    /// <summary>GLSL 330 core chunk fragment shader with <c>0..15</c> static + dynamic held-torch light and fog.</summary>
    public const string ChunkFragmentShader = """
        #version 330 core
        in vec2 vUv;
        flat in float vTileIndex;
        in float vShadeAndAo;
        in float vSkyLight;
        in float vBlockLight;
        in vec3 vCamRelPos;
        in float vDistance;

        uniform sampler2D uAtlas;
        uniform float uSkyBrightness;
        uniform float uHeldTorchLight;
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
            if (texel.a < 0.08)
            {
                discard;
            }

            float dynamicTorch = max(0.0, uHeldTorchLight - length(vCamRelPos));
            float effectiveBlock = max(vBlockLight, dynamicTorch);
            float effectiveLight0To15 = clamp(max(vSkyLight * uSkyBrightness, effectiveBlock), 0.0, 15.0);

            float normL = effectiveLight0To15 / 15.0;
            float curved = normL / (3.0 - 2.0 * normL);
            float minAmbient = mix(0.045, 0.20, uUnderwater);
            float illumination = (minAmbient + curved * (1.0 - minAmbient)) * vShadeAndAo;

            vec3 warmTint = mix(vec3(1.0), vec3(1.04, 0.97, 0.88), clamp(effectiveBlock / 15.0, 0.0, 1.0) * (1.0 - clamp(vSkyLight * uSkyBrightness / 15.0, 0.0, 1.0)));
            vec3 litColor = texel.rgb * warmTint * illumination;
            litColor = mix(litColor, litColor * vec3(0.54, 0.84, 1.05), uUnderwater * 0.72);

            float fogFactor = clamp((vDistance - uFogStart) / max(1.0, uFogEnd - uFogStart), 0.0, 1.0);
            vec3 finalRgb = mix(litColor, uSkyColor, fogFactor);
            FragColor = vec4(finalRgb, texel.a);
        }
        """;
}
