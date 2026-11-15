#version 450

// Shared fragment stage for every dynamic overlay batch. UVs below -2.0 address the entity atlas,
// UVs at or above 0.0 address the block/item atlas, and anything in between is a flat untextured colour.

layout (set = 0, binding = 1) uniform sampler2D uAtlas;
layout (set = 0, binding = 2) uniform sampler2D uEntityAtlas;

layout (push_constant) uniform OverlayPushConstants
{
    mat4 uTransform;
    vec4 uFog; // x = fog start, y = fog end
} uPush;

layout (location = 0) in vec4 vColor;
layout (location = 1) in vec2 vUv;
layout (location = 2) in float vDistance;

layout (location = 0) out vec4 FragColor;

void main()
{
    vec4 col;
    if (vUv.x <= -2.0)
    {
        vec2 entityUv = vec2(-vUv.x - 2.0, vUv.y);
        vec4 eSample = texture(uEntityAtlas, entityUv);
        if (eSample.a < 0.1)
        {
            discard;
        }

        col = vec4(eSample.rgb * vColor.rgb, eSample.a * vColor.a);
    }
    else if (vUv.x >= 0.0)
    {
        vec4 tex = texture(uAtlas, vUv);
        if (tex.a < 0.1)
        {
            discard;
        }

        col = vec4(tex.rgb * vColor.rgb, tex.a * vColor.a);
    }
    else
    {
        col = vColor;
    }

    float fog = clamp((vDistance - uPush.uFog.x) / max(0.01, uPush.uFog.y - uPush.uFog.x), 0.0, 1.0);
    FragColor = vec4(mix(col.rgb, vec3(0.0), fog), col.a);
}
