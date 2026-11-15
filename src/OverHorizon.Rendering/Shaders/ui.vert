#version 450

// Pixel-space 2D HUD vertex stage (compiled to SPIR-V before the game is built).

layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aUv;
layout (location = 2) in vec4 aColor;

layout (push_constant) uniform UiPushConstants
{
    vec2 uViewport;
} uPush;

layout (location = 0) out vec2 vUv;
layout (location = 1) out vec4 vColor;

void main()
{
    vUv = aUv;
    vColor = aColor;
    vec2 ndc = vec2(((aPos.x / uPush.uViewport.x) * 2.0) - 1.0, 1.0 - ((aPos.y / uPush.uViewport.y) * 2.0));
    gl_Position = vec4(ndc, 0.0, 1.0);
}
