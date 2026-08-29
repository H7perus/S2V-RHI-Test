using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace S2V_RHI_Test.RHI
{
    //With VK_EXT_descriptor_heap, these would become resource index and sampler index respectively.
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct DescriptorHandle<T> where T : Resource
    {
        public readonly uint Index;
        public readonly uint padding;

        public DescriptorHandle(uint index, uint padding = 0)
        {
            Index = index;
            this.padding = padding;
        }
    }

    public class Resource
    {
    }
}
