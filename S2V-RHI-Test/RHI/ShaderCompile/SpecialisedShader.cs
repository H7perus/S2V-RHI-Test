using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Vulkan;

using SlangShaderSharp;

namespace S2V_RHI_Test.RHI.ShaderCompile
{
    public readonly record struct VertexInput(
        string SemanticName,
        uint SemanticIndex,
        VkFormat Format,
        uint Location,
        uint Size
    );

    public readonly record struct StructMember(
        string Name,
        string Type,
        uint Offset
    );

    public record struct CompileTimeConstantValue(
        string Name,
        int Value
        );
    public readonly record struct CompileTimeConstant(
        string Name,
        int Min,
        int Max, 
        int Default
        );

    //We should reflect on the compile time constants once
    public sealed record SlangShaderModule(
        IModule Module,
        IReadOnlyList<CompileTimeConstant> CompileTimeConstants
        );

    public sealed record SpecialisedShader(
        ReadOnlyMemory<byte> Spirv,
        IReadOnlyDictionary<VkShaderStageFlags, string> Stages,
        IReadOnlyList<VertexInput> VertexInputs,
        IReadOnlyList<StructMember> PushConstants,
        IReadOnlyList<StructMember> MaterialParameters,

        //we might not want to move this around. This is a leftover from my own RHI. Rest is def needed for pipeline creation though.
        IReadOnlyList<CompileTimeConstantValue> CompileTimeConstantValues

    );
}
