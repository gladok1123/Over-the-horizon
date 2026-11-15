#version 450

// Greedy-meshed voxel terrain fragment stage (compiled to SPIR-V before the game is built).
// Decodes the flat atlas tile index, snaps sampling to exact 16x16 texel centres (no tile bleed), then
// applies sky/block light, held-torch falloff, smooth AO, underwater tint and distance fog.

layout (set = 0, binding = 0) uniform FrameUniforms
{
    mat4 uViewProjection;
    vec4 uFog;      // x = fog start, y = fog end
    vec4 uSky;      // rgb = fog/sky colour, w = sky brightness
    vec4 uLighting; // x = held torch light, y = ambient floor, z = underwater flag
} uFrame;

layout (set = 0, binding = 1) uniform sampler2D uAtlas;

layout (location = 0) in vec2 vUv;
layout (location = 1) flat in float vTileIndex;
layout (location = 2) in float vShadeAndAo;
layout (location = 3) in float vSkyLight;
layout (location = 4) in float vBlockLight;
layout (location = 5) in vec3 vCamRelPos;

layout (location = 0) out vec4 FragColor;

void main()
{
    int tileIdx = int(vTileIndex + 0.5);
    int tileCol = tileIdx & 31;
    int tileRow = tileIdx >> 5;

    // Prevent subpixel overshoot below 0.0 or at exact integer upper quad boundaries (e.g. 16.0 -> fract = 0).
    vec2 clampedUv = max(vUv, vec2(0.0002));
    vec2 nudgedUv = clampedUv - (vec2(0.00025) * step(vec2(0.5), clampedUv));
    vec2 localTexel = clamp(floor(fract(nudgedUv) * 16.0), vec2(0.0), vec2(15.0));
    vec2 atlasUv = (vec2(float(tileCol * 16), float(tileRow * 16)) + localTexel + vec2(0.5)) / 512.0;

    vec4 texel = texture(uAtlas, atlasUv);
    if (texel.a < 0.08)
    {
        discard;
    }

    float underwater = uFrame.uLighting.z;
    float distanceToCamera = length(vCamRelPos);
    float dynamicTorch = max(0.0, uFrame.uLighting.x - distanceToCamera);
    float effectiveBlock = max(vBlockLight, dynamicTorch);
    float effectiveLight0To15 = clamp(max(vSkyLight * uFrame.uSky.w, effectiveBlock), 0.0, 15.0);

    float normL = effectiveLight0To15 / 15.0;
    float curved = normL / (3.0 - (2.0 * normL));
    float minAmbient = max(uFrame.uLighting.y, mix(0.045, 0.20, underwater));
    float illumination = (minAmbient + (curved * (1.0 - minAmbient))) * vShadeAndAo;

    vec3 warmTint = mix(
        vec3(1.0),
        vec3(1.04, 0.97, 0.88),
        clamp(effectiveBlock / 15.0, 0.0, 1.0) * (1.0 - clamp((vSkyLight * uFrame.uSky.w) / 15.0, 0.0, 1.0)));

    vec3 litColor = texel.rgb * warmTint * illumination;
    litColor = mix(litColor, litColor * vec3(0.54, 0.84, 1.05), underwater * 0.72);

    float fogFactor = clamp((distanceToCamera - uFrame.uFog.x) / max(1.0, uFrame.uFog.y - uFrame.uFog.x), 0.0, 1.0);
    FragColor = vec4(mix(litColor, uFrame.uSky.rgb, fogFactor), texel.a);
}
