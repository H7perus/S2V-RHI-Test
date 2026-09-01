using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

using S2V_RHI_Test.RHI.ShaderCompile;

namespace S2V_RHI_Test.RHI
{
    public class PipelineGraphics : Pipeline
    {
        unsafe public PipelineGraphics(SpecialisedShader shader, VkFormat colorTargetFormat = 0, VkFormat depthTargetFormat = 0)
        {

            VkShaderModule shaderModule;
            fixed (byte* pSpirv = shader.Spirv.Span)
            {
                VkShaderModuleCreateInfo shaderModuleInfo = new VkShaderModuleCreateInfo
                {
                    codeSize = (nuint)shader.Spirv.Length,
                    pCode = (uint*)pSpirv
                };

                RenderDevice!.VkDeviceApi.vkCreateShaderModule(shaderModuleInfo, &shaderModule);
            }

            byte[] vertName = Encoding.ASCII.GetBytes("vertMain\0");
            byte[] fragName = Encoding.ASCII.GetBytes("fragMain\0");

            VkPipelineShaderStageCreateInfo[] shaderModules = new VkPipelineShaderStageCreateInfo[2];


            //low safety. We could really check and decide on what we prefer. Potentially even based on global state, like mesh shading support.
            var bitmask = VkShaderStageFlags.Vertex | VkShaderStageFlags.MeshEXT | VkShaderStageFlags.Fragment;

            var matched = shader.Stages
                .Where(s => (s.Key & bitmask) != 0)
                .ToArray();

            using var names = new VkStringArray(matched.Select(s => s.Value).ToArray());

            var index = 0;
            foreach (var stage in matched)
            {
                shaderModules[index].sType = VkStructureType.PipelineShaderStageCreateInfo;
                shaderModules[index].stage = stage.Key;
                shaderModules[index].module = shaderModule;
                shaderModules[index].pName = *((byte**)names + index);

                index++;
            }


            VkPipelineRenderingCreateInfo renderingCreateInfo = new VkPipelineRenderingCreateInfo
            {
                colorAttachmentCount = Math.Min((uint)colorTargetFormat, 1),
                pColorAttachmentFormats = &colorTargetFormat,
                depthAttachmentFormat = depthTargetFormat
            };

            


            

            var vertexInputAttributes = new VkVertexInputAttributeDescription[shader.VertexInputs.Count];

            uint attributeStride = 0;

            for (int i = 0; i < shader.VertexInputs.Count; i++)
            {
                vertexInputAttributes[i].location = shader.VertexInputs[i].Location;

                vertexInputAttributes[i].format = shader.VertexInputs[i].Format;

                if (shader.VertexInputs[i].SemanticName == "POSITION" && shader.VertexInputs[i].SemanticIndex == 0)
                {
                    vertexInputAttributes[i].binding = 0;
                    vertexInputAttributes[i].offset = 0;
                }
                else
                {
                    vertexInputAttributes[i].binding = 1;
                    vertexInputAttributes[i].offset = attributeStride;
                    attributeStride += shader.VertexInputs[i].Size;
                }

                
            }
            //pos buffer
            VkVertexInputBindingDescription vertexPositionBinding = new()
            {
                binding = 0,
                stride = 12,
                inputRate = VkVertexInputRate.Vertex,
            };
            //attrib buffer
            VkVertexInputBindingDescription vertexAttributeBinding = new()
            {
                binding = 1,
                stride = attributeStride,
                inputRate = VkVertexInputRate.Vertex,
            };

            VkVertexInputBindingDescription[] vertexBindingDescriptions = [vertexPositionBinding, vertexAttributeBinding];

            fixed (VkVertexInputAttributeDescription* pVertexAttributes = vertexInputAttributes)
            fixed (VkVertexInputBindingDescription* pVertexBindings = vertexBindingDescriptions)
            fixed (VkPipelineShaderStageCreateInfo* pShaderStageCreateInfos = shaderModules)
            {
                VkPipelineVertexInputStateCreateInfo vertexInputStateCreateInfo = new() 
                {
                    vertexAttributeDescriptionCount = (uint)vertexInputAttributes.Length,
                    pVertexAttributeDescriptions = pVertexAttributes,
                    vertexBindingDescriptionCount = (uint)vertexBindingDescriptions.Length,
                    pVertexBindingDescriptions = pVertexBindings,
                };

                // --- Input assembly: how vertices are grouped into primitives ---
                VkPipelineInputAssemblyStateCreateInfo inputAssemblyStateCreateInfo = new()
                {
                    topology = VkPrimitiveTopology.TriangleStrip,
                    primitiveRestartEnable = false
                };

                // --- Viewport/scissor: counts only, actual values set dynamically at draw time ---
                VkPipelineViewportStateCreateInfo viewportStateCreateInfo = new()
                {
                    viewportCount = 1,
                    pViewports = null,
                    scissorCount = 1,
                    pScissors = null
                };

                VkDynamicState* dynamicStates = stackalloc VkDynamicState[]
                {
                    VkDynamicState.Viewport,
                    VkDynamicState.Scissor,
                };

                VkPipelineDynamicStateCreateInfo dynamicStateCreateInfo = new()
                {
                    dynamicStateCount = 2,
                    pDynamicStates = dynamicStates
                };

                VkPipelineMultisampleStateCreateInfo msStateCreateinfo = new()
                {
                    rasterizationSamples = VkSampleCountFlags.Count1,
                    sampleShadingEnable = false,
                };

                VkPipelineRasterizationStateCreateInfo pipelineRasterizationStateCreateInfo = new()
                {
                    polygonMode = VkPolygonMode.Fill,
                    lineWidth = 1.0f,
                    cullMode = VkCullModeFlags.None,
                    frontFace = VkFrontFace.CounterClockwise,
                    depthClampEnable = false,
                    rasterizerDiscardEnable = false,
                    depthBiasEnable = false
                };

                // --- Color blend: one attachment, blending disabled, write all channels ---
                VkPipelineColorBlendAttachmentState colorBlendAttachmentState = new()
                {
                    blendEnable = false,
                    colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G |
                                      VkColorComponentFlags.B | VkColorComponentFlags.A
                };

                VkPipelineColorBlendStateCreateInfo colorBlendStateCreateInfo = new()
                {
                    logicOpEnable = false,
                    attachmentCount = 1,
                    pAttachments = &colorBlendAttachmentState
                };

                VkGraphicsPipelineCreateInfo pipelineInfo = new VkGraphicsPipelineCreateInfo
                {
                    pNext = &renderingCreateInfo,
                    stageCount = 2,
                    pStages = pShaderStageCreateInfos,
                    layout = RenderDevice.SharedPipelineLayout,
                    pVertexInputState = &vertexInputStateCreateInfo,
                    pInputAssemblyState = &inputAssemblyStateCreateInfo,
                    pViewportState = &viewportStateCreateInfo,
                    pRasterizationState = &pipelineRasterizationStateCreateInfo,
                    pMultisampleState = &msStateCreateinfo,
                    pColorBlendState = &colorBlendStateCreateInfo,
                    pDynamicState = &dynamicStateCreateInfo,
                };

                var result = RenderDevice!.VkDeviceApi.vkCreateGraphicsPipeline(pipelineInfo, out var pipeline);

                HandlePipeline = pipeline;
            }

            RenderDevice!.VkDeviceApi.vkDestroyShaderModule(shaderModule);
        }
    }
}
