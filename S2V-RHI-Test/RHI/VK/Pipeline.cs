using System;
using System.Collections.Generic;
using System.Text;


using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace S2V_RHI_Test.RHI.VK
{
    public class Pipeline : IDisposable
    {
        public VkPipeline VkPipeline { get; protected set; }
        public VkPipelineLayout VkPipelineLayout { get; protected set; }

        unsafe public Pipeline()
        {
            uint[] VertexShaderSpirv = new uint[]
        {
            0x07230203, 0x00010000, 0x0008000B, 0x00000034, 0x00000000, 0x00020011,
            0x00000001, 0x0006000B, 0x00000001, 0x4C534C47, 0x6474732E, 0x3035342E,
            0x00000000, 0x0003000E, 0x00000000, 0x00000001, 0x0008000F, 0x00000000,
            0x00000004, 0x6E69616D, 0x00000000, 0x00000020, 0x00000024, 0x0000002F,
            0x00030003, 0x00000002, 0x000001C2, 0x00040005, 0x00000004, 0x6E69616D,
            0x00000000, 0x00050005, 0x0000000C, 0x69736F70, 0x6E6F6974, 0x00000073,
            0x00040005, 0x00000017, 0x6F6C6F63, 0x00007372, 0x00060005, 0x0000001E,
            0x505F6C67, 0x65567265, 0x78657472, 0x00000000, 0x00060006, 0x0000001E,
            0x00000000, 0x505F6C67, 0x7469736F, 0x006E6F69, 0x00070006, 0x0000001E,
            0x00000001, 0x505F6C67, 0x746E696F, 0x657A6953, 0x00000000, 0x00070006,
            0x0000001E, 0x00000002, 0x435F6C67, 0x4470696C, 0x61747369, 0x0065636E,
            0x00070006, 0x0000001E, 0x00000003, 0x435F6C67, 0x446C6C75, 0x61747369,
            0x0065636E, 0x00030005, 0x00000020, 0x00000000, 0x00060005, 0x00000024,
            0x565F6C67, 0x65747265, 0x646E4978, 0x00007865, 0x00050005, 0x0000002F,
            0x67617266, 0x6F6C6F43, 0x00000072, 0x00030047, 0x0000001E, 0x00000002,
            0x00050048, 0x0000001E, 0x00000000, 0x0000000B, 0x00000000, 0x00050048,
            0x0000001E, 0x00000001, 0x0000000B, 0x00000001, 0x00050048, 0x0000001E,
            0x00000002, 0x0000000B, 0x00000003, 0x00050048, 0x0000001E, 0x00000003,
            0x0000000B, 0x00000004, 0x00040047, 0x00000024, 0x0000000B, 0x0000002A,
            0x00040047, 0x0000002F, 0x0000001E, 0x00000000, 0x00020013, 0x00000002,
            0x00030021, 0x00000003, 0x00000002, 0x00030016, 0x00000006, 0x00000020,
            0x00040017, 0x00000007, 0x00000006, 0x00000002, 0x00040015, 0x00000008,
            0x00000020, 0x00000000, 0x0004002B, 0x00000008, 0x00000009, 0x00000003,
            0x0004001C, 0x0000000A, 0x00000007, 0x00000009, 0x00040020, 0x0000000B,
            0x00000006, 0x0000000A, 0x0004003B, 0x0000000B, 0x0000000C, 0x00000006,
            0x0004002B, 0x00000006, 0x0000000D, 0x00000000, 0x0004002B, 0x00000006,
            0x0000000E, 0xBF000000, 0x0005002C, 0x00000007, 0x0000000F, 0x0000000D,
            0x0000000E, 0x0004002B, 0x00000006, 0x00000010, 0x3F000000, 0x0005002C,
            0x00000007, 0x00000011, 0x00000010, 0x00000010, 0x0005002C, 0x00000007,
            0x00000012, 0x0000000E, 0x00000010, 0x0006002C, 0x0000000A, 0x00000013,
            0x0000000F, 0x00000011, 0x00000012, 0x00040017, 0x00000014, 0x00000006,
            0x00000003, 0x0004001C, 0x00000015, 0x00000014, 0x00000009, 0x00040020,
            0x00000016, 0x00000006, 0x00000015, 0x0004003B, 0x00000016, 0x00000017,
            0x00000006, 0x0004002B, 0x00000006, 0x00000018, 0x3F800000, 0x0006002C,
            0x00000014, 0x00000019, 0x00000018, 0x00000010, 0x0000000D, 0x0006002C,
            0x00000015, 0x0000001A, 0x00000019, 0x00000019, 0x00000019, 0x00040017,
            0x0000001B, 0x00000006, 0x00000004, 0x0004002B, 0x00000008, 0x0000001C,
            0x00000001, 0x0004001C, 0x0000001D, 0x00000006, 0x0000001C, 0x0006001E,
            0x0000001E, 0x0000001B, 0x00000006, 0x0000001D, 0x0000001D, 0x00040020,
            0x0000001F, 0x00000003, 0x0000001E, 0x0004003B, 0x0000001F, 0x00000020,
            0x00000003, 0x00040015, 0x00000021, 0x00000020, 0x00000001, 0x0004002B,
            0x00000021, 0x00000022, 0x00000000, 0x00040020, 0x00000023, 0x00000001,
            0x00000021, 0x0004003B, 0x00000023, 0x00000024, 0x00000001, 0x00040020,
            0x00000026, 0x00000006, 0x00000007, 0x00040020, 0x0000002C, 0x00000003,
            0x0000001B, 0x00040020, 0x0000002E, 0x00000003, 0x00000014, 0x0004003B,
            0x0000002E, 0x0000002F, 0x00000003, 0x00040020, 0x00000031, 0x00000006,
            0x00000014, 0x00050036, 0x00000002, 0x00000004, 0x00000000, 0x00000003,
            0x000200F8, 0x00000005, 0x0003003E, 0x0000000C, 0x00000013, 0x0003003E,
            0x00000017, 0x0000001A, 0x0004003D, 0x00000021, 0x00000025, 0x00000024,
            0x00050041, 0x00000026, 0x00000027, 0x0000000C, 0x00000025, 0x0004003D,
            0x00000007, 0x00000028, 0x00000027, 0x00050051, 0x00000006, 0x00000029,
            0x00000028, 0x00000000, 0x00050051, 0x00000006, 0x0000002A, 0x00000028,
            0x00000001, 0x00070050, 0x0000001B, 0x0000002B, 0x00000029, 0x0000002A,
            0x0000000D, 0x00000018, 0x00050041, 0x0000002C, 0x0000002D, 0x00000020,
            0x00000022, 0x0003003E, 0x0000002D, 0x0000002B, 0x0004003D, 0x00000021,
            0x00000030, 0x00000024, 0x00050041, 0x00000031, 0x00000032, 0x00000017,
            0x00000030, 0x0004003D, 0x00000014, 0x00000033, 0x00000032, 0x0003003E,
            0x0000002F, 0x00000033, 0x000100FD, 0x00010038
        };

            uint[] FragmentShaderSpirv = new uint[]
        {
            0x07230203, 0x00010000, 0x0008000B, 0x00000013, 0x00000000, 0x00020011,
            0x00000001, 0x0006000B, 0x00000001, 0x4C534C47, 0x6474732E, 0x3035342E,
            0x00000000, 0x0003000E, 0x00000000, 0x00000001, 0x0007000F, 0x00000004,
            0x00000004, 0x6E69616D, 0x00000000, 0x00000009, 0x0000000C, 0x00030010,
            0x00000004, 0x00000007, 0x00030003, 0x00000002, 0x000001C2, 0x00040005,
            0x00000004, 0x6E69616D, 0x00000000, 0x00050005, 0x00000009, 0x4374756F,
            0x726F6C6F, 0x00000000, 0x00050005, 0x0000000C, 0x67617266, 0x6F6C6F43,
            0x00000072, 0x00040047, 0x00000009, 0x0000001E, 0x00000000, 0x00040047,
            0x0000000C, 0x0000001E, 0x00000000, 0x00020013, 0x00000002, 0x00030021,
            0x00000003, 0x00000002, 0x00030016, 0x00000006, 0x00000020, 0x00040017,
            0x00000007, 0x00000006, 0x00000004, 0x00040020, 0x00000008, 0x00000003,
            0x00000007, 0x0004003B, 0x00000008, 0x00000009, 0x00000003, 0x00040017,
            0x0000000A, 0x00000006, 0x00000003, 0x00040020, 0x0000000B, 0x00000001,
            0x0000000A, 0x0004003B, 0x0000000B, 0x0000000C, 0x00000001, 0x0004002B,
            0x00000006, 0x0000000E, 0x3F800000, 0x00050036, 0x00000002, 0x00000004,
            0x00000000, 0x00000003, 0x000200F8, 0x00000005, 0x0004003D, 0x0000000A,
            0x0000000D, 0x0000000C, 0x00050051, 0x00000006, 0x0000000F, 0x0000000D,
            0x00000000, 0x00050051, 0x00000006, 0x00000010, 0x0000000D, 0x00000001,
            0x00050051, 0x00000006, 0x00000011, 0x0000000D, 0x00000002, 0x00070050,
            0x00000007, 0x00000012, 0x0000000F, 0x00000010, 0x00000011, 0x0000000E,
            0x0003003E, 0x00000009, 0x00000012, 0x000100FD, 0x00010038,
        };

            VkShaderModule vertexShaderModule;
            VkShaderModule fragmentShaderModule;
            fixed (uint* pVertSpirv = VertexShaderSpirv)
            fixed (uint* pFragSpirv = FragmentShaderSpirv)
            {
                VkShaderModuleCreateInfo vertexModuleInfo = new VkShaderModuleCreateInfo
                {
                    codeSize = (nuint)VertexShaderSpirv.Length * 4,
                    pCode = pVertSpirv
                };
                VkShaderModuleCreateInfo fragmentModuleInfo = new VkShaderModuleCreateInfo
                {
                    codeSize = (nuint)FragmentShaderSpirv.Length * 4,
                    pCode = pFragSpirv
                };

                RenderDevice!.VkDeviceApi.vkCreateShaderModule(vertexModuleInfo, &vertexShaderModule);
                RenderDevice!.VkDeviceApi.vkCreateShaderModule(fragmentModuleInfo, &fragmentShaderModule);
            }

            byte[] stageName = Encoding.ASCII.GetBytes("main\0"); // null-terminated for the C string

            VkPipelineShaderStageCreateInfo[] shaderModules = new VkPipelineShaderStageCreateInfo[2];

            fixed (byte* pStageName = stageName)
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
                    module = vertexShaderModule,
                    pName = pStageName
                };

                shaderModules[1] = new VkPipelineShaderStageCreateInfo
                {
                    sType = VkStructureType.PipelineShaderStageCreateInfo,
                    stage = VkShaderStageFlags.Fragment,
                    module = fragmentShaderModule,
                    pName = pStageName
                };

                VkPipelineLayoutCreateInfo layoutCreateInfo = new()
                {
                    setLayoutCount = 0,
                    pushConstantRangeCount = 0
                };

                RenderDevice!.VkDeviceApi.vkCreatePipelineLayout(layoutCreateInfo, out var pipelineLayout);

                VkPipelineVertexInputStateCreateInfo vertexInputStateCreateInfo = new() { };

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
                    layout = pipelineLayout,
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
                VkPipeline = pipeline;
                VkPipelineLayout = pipelineLayout;
            }

            // Shader modules aren't needed after pipeline creation
            RenderDevice!.VkDeviceApi.vkDestroyShaderModule(vertexShaderModule);
            RenderDevice!.VkDeviceApi.vkDestroyShaderModule(fragmentShaderModule);

        }

        unsafe public virtual void Dispose()
        {
            if (VkPipeline.Handle != 0)
                RenderDevice!.VkDeviceApi.vkDestroyPipeline(VkPipeline, null);

            if (VkPipelineLayout.Handle != 0)
                RenderDevice!.VkDeviceApi.vkDestroyPipelineLayout(VkPipelineLayout, null);

            GC.SuppressFinalize(this);
        }
    }
}