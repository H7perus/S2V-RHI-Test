using System;
using System.Drawing;
using Vortice.Vulkan;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Vortice.Vulkan.Vma;
using static Vortice.Vulkan.Vulkan;

namespace S2V_RHI_Test.RHI
{
    public class Buffer : Resource
    {
        public VkBuffer Handle { get; private set; }
        public VmaAllocation VmaAllocation { get; private set; }
        public ulong Size { get; private set; }

        public uint BindlessIndex { get; private set; }

        public DescriptorHandle<Buffer> DescriptorHandle => new DescriptorHandle<Buffer>(BindlessIndex);

        public Buffer(ulong size, VkBufferUsageFlags usage, VmaMemoryUsage memoryUsage, VmaAllocationCreateFlags allocationFlags = VmaAllocationCreateFlags.None)
        {
            Size = size;

            

            CreateBuffer(usage, memoryUsage, allocationFlags);

            //hack
            if(usage == VkBufferUsageFlags.UniformBuffer)
                BindlessIndex = RenderDevice!.GetBindlessSlot(VkDescriptorType.UniformBuffer, Handle);

        }

        unsafe void CreateBuffer(VkBufferUsageFlags usage, VmaMemoryUsage memoryUsage, VmaAllocationCreateFlags allocationFlags)
        {            
            //H7per: NOTE: I am making the sharing mode "Exclusive". This *may* be problematic for transfer buffers later, but thats a problem for future us
            VkBufferCreateInfo bufferCreateInfo = new()
            {
                size = Size,
                usage = usage,
                sharingMode = VkSharingMode.Exclusive,
            };

            VmaAllocationCreateInfo allocationCreateInfo = new()
            {
                flags = allocationFlags,
                usage = memoryUsage,
            };

            vmaCreateBuffer(RenderDevice!.VmaAllocator, bufferCreateInfo, allocationCreateInfo, out var buffer, out var allocation);
            Handle = buffer;
            VmaAllocation = allocation;
        }

        public unsafe void* Map()
        {
            void* data;
            vmaMapMemory(RenderDevice!.VmaAllocator, VmaAllocation, &data);
            return data;
        }

        public void Unmap()
        {
            vmaUnmapMemory(RenderDevice!.VmaAllocator, VmaAllocation);
        }

        public unsafe void SetData<T>(ReadOnlySpan<T> data, ulong offset = 0) where T : unmanaged
        {
            // TODO: Map, copy, Unmap (or vmaCopyMemoryToAllocation if HOST_VISIBLE + persistently mapped)
        }

        public void Flush(ulong offset = 0, ulong size = VK_WHOLE_SIZE)
        {
            // TODO: vmaFlushAllocation(device.VmaAllocator, VmaAllocation, offset, size) — needed for non-coherent memory
        }

    }
}