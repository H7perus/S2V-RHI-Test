using System;
using System.Collections.Generic;
using System.Text;


using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace S2V_RHI_Test.RHI
{
    public class Pipeline : IDisposable
    {
        public VkPipeline HandlePipeline { get; protected set; }

        public Pipeline()
        {
            

        }

        unsafe public virtual void Dispose()
        {
            if (HandlePipeline.Handle != 0)
                RenderDevice!.VkDeviceApi.vkDestroyPipeline(HandlePipeline, null);

            GC.SuppressFinalize(this);
        }
    }
}