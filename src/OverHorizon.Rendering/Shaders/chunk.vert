#version 450

// Greedy-meshed voxel terrain vertex stage (compiled to SPIR-V before the game is built).
// Positions are section-local (0..16) and shifted into camera-relative space by a push constant, so the
// whole section batch can be drawn with a single descriptor set and one tiny push per section.

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aUv;
layout (location = 2) in float aTileIndex;
layout (location = 3) in float aShadeAndAo;
layout (location = 4) in float aLightLevel;

layout (set = 0, binding = 0) uniform FrameUniforms
{
    mat4 uViewProjection;
    vec4 uFog;      // x = fog start, y = fog end
    vec4 uSky;      // rgb = fog/sky colour, w = sky brightness
    vec4 uLighting; // x = held torch light, y = ambient floor, z = underwater flag
} uFrame;

layout (push_constant) uniform SectionPushConstants
{
    vec4 uSectionOffset;
} uPush;

layout (location = 0) out vec2 vUv;
layout (location = 1) flat out float vTileIndex;
layout (location = 2) out float vShadeAndAo;
layout (location = 3) out float vSkyLight;
layout (location = 4) out float vBlockLight;
layout (location = 5) out vec3 vCamRelPos;

void main()
{
    vec3 cameraRelativePos = uPush.uSectionOffset.xyz + aPosition;
    int packedLight = int(aLightLevel + 0.5);

    vUv = aUv;
    vTileIndex = aTileIndex;
    vShadeAndAo = aShadeAndAo;
    vSkyLight = float((packedLight >> 4) & 15);
    vBlockLight = float(packedLight & 15);
    vCamRelPos = cameraRelativePos;

    gl_Position = uFrame.uViewProjection * vec4(cameraRelativePos, 1.0);
}
