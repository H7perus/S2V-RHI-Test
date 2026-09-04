module testShader;

import Core; //because fuck slang (jk)

struct VertexOutput
{
    float4 pos : SV_Position;
    float3 color;
}

static const float3 pos[3] = {
    float3(0, -0.5, 0),
    float3(0.5, 0.5, 0),
    float3(-0.5, 0.5, 0)
};

[shader("vertex")]
VertexOutput vertMain(float3 vPos: POSITION, float3 vColor: COLOR, uint vertexIndex: SV_VulkanVertexID)
{
    VertexOutput out;
    out.color = vColor;
    out.pos = float4(vPos, 1);

    return out;

}

[shader("fragment")]
float4 fragMain(VertexOutput input) : SV_Target
{
    return float4(input.color, 1.0);
}