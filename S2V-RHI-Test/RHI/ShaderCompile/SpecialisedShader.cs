using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Vulkan;

namespace S2V_RHI_Test.RHI.ShaderCompile
{
    public readonly record struct VertexInput(
        string SemanticName,
        uint SemanticIndex,
        VkFormat Format,
        uint Location
    );

    public readonly record struct StructMember(
        string name,
        string type,
        uint offset
    );

    public readonly record struct CompileTimeConstantValue(
        string name,
        int value
        );

    public sealed record SpecializedShader(
        ReadOnlyMemory<uint> Spirv,
        IReadOnlyList<VertexInput> VertexInputs,
        IReadOnlyList<StructMember> PushConstants,
        IReadOnlyList<StructMember> MaterialParameters,

        //we might not want to move this around. This is a leftover from my own RHI. Rest is def needed for pipeline creation though.
        IReadOnlyList<CompileTimeConstantValue> CompileTimeConstants

    );
}
