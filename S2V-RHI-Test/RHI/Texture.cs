using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection.Metadata;
using System.Text;
using Vortice.Vulkan;
using static System.Net.Mime.MediaTypeNames;
using static Vortice.Vulkan.Vma;
using static Vortice.Vulkan.Vulkan;

namespace S2V_RHI_Test.RHI
{
    public class Texture : Resource
    {
        public VkImage ImageHandle { get; private set; }
        public VkImageView ImageViewHandle { get; private set; }

        public VkSampler SamplerHandle { get; private set; }
        public VmaAllocation VmaAllocation { get; private set; }

        public uint BindlessIndex { get; private set; }

        public DescriptorHandle<Texture> DescriptorHandle => new DescriptorHandle<Texture>(BindlessIndex);

        public unsafe Texture(uint width, uint height)
        {
            VkImageCreateInfo imageInfo = new()
            {
                sType = VkStructureType.ImageCreateInfo,
                imageType = VkImageType.Image2D,
                format = VkFormat.R8G8B8A8Unorm,
                extent = new VkExtent3D(width, height, 1),
                mipLevels = 3,
                arrayLayers = 1,
                samples = VkSampleCountFlags.Count1,
                tiling = VkImageTiling.Optimal,

                usage =
                VkImageUsageFlags.TransferDst |
                VkImageUsageFlags.Sampled,

                sharingMode = VkSharingMode.Exclusive,

                initialLayout = VkImageLayout.TransferDstOptimal
            };

            VmaAllocationCreateInfo allocationCreateInfo = new()
            {
                usage = VmaMemoryUsage.AutoPreferDevice,
            };

            VkResult result = vmaCreateImage(RenderDevice!.VmaAllocator, imageInfo, allocationCreateInfo, out var image, out var allocation);

            ImageHandle = image;
            VmaAllocation = allocation;

            var imageViewInfo = new VkImageViewCreateInfo
            {
                image = ImageHandle,
                viewType = VkImageViewType.Image2D,
                format = VkFormat.R8G8B8A8Unorm,
                components = VkComponentMapping.Rgba,
                subresourceRange = new VkImageSubresourceRange { baseMipLevel = 0, aspectMask = VkImageAspectFlags.Color, levelCount = 3, layerCount = 1, baseArrayLayer = 0 }
            };

            RenderDevice!.VkDeviceApi.vkCreateImageView(imageViewInfo, out var viewHandle);

            ImageViewHandle = viewHandle;

            var samplerInfo = new VkSamplerCreateInfo
            {
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                mipmapMode = VkSamplerMipmapMode.Linear,
                mipLodBias = 0,
                anisotropyEnable = false,
                minLod = 0,
                maxLod = 1000,
            };



            SamplerHandle = RenderDevice!.CreateSampler(samplerInfo);

            BindlessIndex = RenderDevice!.GetBindlessSlot(VkDescriptorType.CombinedImageSampler, ImageViewHandle, SamplerHandle);

        }

        public void SetSampler(VkSampler sampler)
        {
            SamplerHandle = sampler;
            RenderDevice!.UpdateBindlessCombinedSampler(ImageViewHandle, sampler, BindlessIndex);
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
    }
}
