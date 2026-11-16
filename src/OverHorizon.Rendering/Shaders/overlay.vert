#version 450

// Shared vertex stage for every dynamic overlay batch: clouds, void floor, block selection, mobs, drops,
// particles, the held viewmodel and the inventory player preview (compiled to SPIR-V before the game is built).

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec4 aColor;
layout (location = 2) in vec2 aUv;

layout (push_constant) uniform OverlayPushConstants
{
    mat4 uTransform;
    vec4 uFog; // x = fog start, y = fog end
    vec4 uFogColor; // rgb = colour distant geometry fades towards, a unused
} uPush;

layout (location = 0) out vec4 vColor;
layout (location = 1) out vec2 vUv;
layout (location = 2) out float vDistance;

void main()
{
    vColor = aColor;
    vUv = aUv;
    vDistance = length(aPosition);
    gl_Position = uPush.uTransform * vec4(aPosition, 1.0);
}
