// triangle.slang
// Orange triangle generated purely from the vertex index — no vertex/index buffers needed.

import Core;

struct VSOutput
{
    float4 position : SV_Position;
    float3 color : COLOR0;
};

static const float2 positions[3] = float2[3](
    float2(0.0, -0.5),
    float2(0.5, 0.5),
    float2(-0.5, 0.5)
);

static const float3 colors[3] = float3[3](
    float3(1.0, 0.0, 0.5),
    float3(1.0, 0.5, 0.0),
    float3(1.0, 0.5, 0.0)
);

[shader("vertex")]
VSOutput vertMain(uint vertexIndex: SV_VulkanVertexID)
{
    VSOutput output;
    output.position = float4(positions[vertexIndex], 0.0, 1.0);
    output.color = colors[vertexIndex];
    return output;
}

[shader("fragment")]
float4 fragMain(VSOutput input) : SV_Target
{
    return float4(input.color, 1.0);
}