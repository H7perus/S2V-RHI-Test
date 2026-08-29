using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Vulkan;

namespace S2V_RHI_Test.RHI
{
    public class PipelineGraphics : Pipeline
    {
        unsafe public PipelineGraphics(byte[] spirv)
        {

            VkShaderModule shaderModule;
            fixed (byte* pSpirv = spirv)
            {
                VkShaderModuleCreateInfo shaderModuleInfo = new VkShaderModuleCreateInfo
                {
                    codeSize = (nuint)spirv.Length,
                    pCode = (uint*)pSpirv
                };

                RenderDevice!.VkDeviceApi.vkCreateShaderModule(shaderModuleInfo, &shaderModule);
            }

            byte[] vertName = Encoding.ASCII.GetBytes("vertMain\0");
            byte[] fragName = Encoding.ASCII.GetBytes("fragMain\0");

            VkPipelineShaderStageCreateInfo[] shaderModules = new VkPipelineShaderStageCreateInfo[2];

            fixed (byte* pVertName = vertName)
            fixed (byte* pFragName = fragName)
            fixed (VkPipelineShaderStageCreateInfo* pShaderStageCreateInfos = shaderModules)
            {
                VkFormat renderTargetFormat = VkFormat.B8G8R8A8Unorm;

                VkPipelineRenderingCreateInfo renderingCreateInfo = new VkPipelineRenderingCreateInfo
                {
                    colorAttachmentCount = 1,
                    pColorAttachmentFormats = &renderTargetFormat
                };

                shaderModules[0] = new VkPipelineShaderStageCreateInfo
                {
                    sType = VkStructureType.PipelineShaderStageCreateInfo,
                    stage = VkShaderStageFlags.Vertex,
                    module = shaderModule,
                    pName = pVertName
                };

                shaderModules[1] = new VkPipelineShaderStageCreateInfo
                {
                    sType = VkStructureType.PipelineShaderStageCreateInfo,
                    stage = VkShaderStageFlags.Fragment,
                    module = shaderModule,
                    pName = pFragName
                };

                VkPipelineVertexInputStateCreateInfo vertexInputStateCreateInfo = new() { };


                //This is for the vertex buffer
                VkVertexInputBindingDescription vertexInputBindingDescription = new()
                {
                    binding = 0,
                    stride = 12,
                    inputRate = VkVertexInputRate.Vertex,
                };

                VkVertexInputAttributeDescription vertexInputAttributeDescription = new()
                {
                    //location in the shader
                    location = 0,
                    //bound to what buffer (binding 0)
                    binding = 0,
                    //with what format
                    format = VkFormat.R32G32B32Sfloat,
                    // at what offset in the bound buffers element (i.e. N * stride + offset for each vertex's value)
                    offset = 0
                };

                vertexInputStateCreateInfo.vertexAttributeDescriptionCount = 1;
                vertexInputStateCreateInfo.pVertexAttributeDescriptions = &vertexInputAttributeDescription;

                vertexInputStateCreateInfo.vertexBindingDescriptionCount = 1;
                vertexInputStateCreateInfo.pVertexBindingDescriptions = &vertexInputBindingDescription;

                // --- Input assembly: how vertices are grouped into primitives ---
                VkPipelineInputAssemblyStateCreateInfo inputAssemblyStateCreateInfo = new()
                {
                    topology = VkPrimitiveTopology.TriangleList,
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

                VkDynamicState* dynamicStates = stackalloc VkDynamicState[2]
                {
                    VkDynamicState.Viewport,
                    VkDynamicState.Scissor
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

                // Store handles for later use / cleanup — adjust field names to match your class
                HandlePipeline = pipeline;
            }

            // Shader modules aren't needed after pipeline creation
            RenderDevice!.VkDeviceApi.vkDestroyShaderModule(shaderModule);
        }
    }
}
