#version 450

// Pixel-space 2D HUD fragment stage. UVs below -2.5 address the 23w13a GUI sheet atlas, UVs below -0.5
// address the item/block sprite atlas, and everything else addresses the bitmap font atlas.

layout (set = 0, binding = 1) uniform sampler2D uFontAtlas;
layout (set = 0, binding = 2) uniform sampler2D uItemAtlas;
layout (set = 0, binding = 3) uniform sampler2D uGuiAtlas;

layout (location = 0) in vec2 vUv;
layout (location = 1) in vec4 vColor;

layout (location = 0) out vec4 FragColor;

void main()
{
    vec4 sampleCol;
    if (vUv.x <= -2.5)
    {
        sampleCol = texture(uGuiAtlas, -vUv - vec2(3.0));
    }
    else if (vUv.x <= -0.5)
    {
        sampleCol = texture(uItemAtlas, -vUv - vec2(1.0));
    }
    else
    {
        sampleCol = texture(uFontAtlas, vUv);
    }

    if (sampleCol.a < 0.08)
    {
        discard;
    }

    FragColor = sampleCol * vColor;
}
