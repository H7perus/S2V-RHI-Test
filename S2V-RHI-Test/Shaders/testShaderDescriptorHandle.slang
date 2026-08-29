// triangle.slang
// Orange triangle generated purely from the vertex index — no vertex/index buffers needed.

import Core;

[ConstantRange(0, 1)]
extern const static int isPurple = 0;

struct VSOutput
{
    float4 position : SV_Position;
    float3 color : COLOR0;
};

struct Parameters
{
    float4 colors[4];
    float4x4 viewPerspective;
}

struct PushData
{
    DescriptorHandle<ConstantBuffer<Parameters>> buffer;
}

[vk::push_constant]
PushData pc;

[shader("vertex")]
VSOutput vertMain(float3 vPos, uint vertexIndex: SV_VulkanVertexID)
{
    const var P = *pc.buffer;

    VSOutput output;

    sizeof(Parameters);

    var transformed = mul(P.viewPerspective, float4(vPos.xyz, 1.0));

    output.position = transformed;

    output.color = P.colors[vertexIndex].xyz;
    if(isPurple == 1)
        output.color = P.colors[vertexIndex].yzx;
    return output;
}

[shader("fragment")]
float4 fragMain(VSOutput input) : SV_Target
{
    return float4(input.color, 1.0);
}