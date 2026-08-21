using System;
using System.Collections.Generic;
using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;
using static S2vDevice;

namespace S2V_RHI_Test.RHI.VK
{
    internal unsafe class Swapchain : IDisposable
    {
        private VkSwapchainKHR _swapchain;

        private uint _currentImageIndex = new();
        private readonly List<VkImage> _images = new();
        private readonly List<VkImageView> _imageViews = new();

        public VkSwapchainKHR Handle => _swapchain;

        public IReadOnlyList<VkImage> Images => _images;
        public IReadOnlyList<VkImageView> ImageViews => _imageViews;

        public VkPresentModeKHR PresentMode { get; private set; }
        public VkSurfaceFormatKHR SurfaceFormat { get; private set; }
        public VkExtent2D Extent { get; private set; }

        public Swapchain(uint width, uint height)
        {
            Create(width, height);
        }

        private void Create(uint width, uint height)
        {
            var device = RenderDevice
                ?? throw new InvalidOperationException("S2vDevice has not been initialized.");

            device.VkInstanceApi.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
                device.VkPhysicalDevice,
                device.VkSurfaceKHR,
                out var capabilities);

            uint formatCount = 0;

            device.VkInstanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(
                device.VkPhysicalDevice,
                device.VkSurfaceKHR,
                &formatCount,
                null);

            if (formatCount == 0)
                throw new Exception("No Vulkan surface formats are available.");

            var formats = new VkSurfaceFormatKHR[formatCount];

            fixed (VkSurfaceFormatKHR* formatsPtr = formats)
            {
                device.VkInstanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(
                    device.VkPhysicalDevice,
                    device.VkSurfaceKHR,
                    &formatCount,
                    formatsPtr);
            }

            SurfaceFormat = ChooseSurfaceFormat(formats);

            uint presentModeCount = 0;

            device.VkInstanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(
                device.VkPhysicalDevice,
                device.VkSurfaceKHR,
                &presentModeCount,
                null);

            if (presentModeCount == 0)
                throw new Exception("No Vulkan present modes are available.");

            var presentModes = new VkPresentModeKHR[presentModeCount];

            fixed (VkPresentModeKHR* presentModesPtr = presentModes)
            {
                device.VkInstanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(
                    device.VkPhysicalDevice,
                    device.VkSurfaceKHR,
                    &presentModeCount,
                    presentModesPtr);
            }

            var presentMode = ChoosePresentMode(presentModes);

            Extent = ChooseExtent(capabilities, width, height);
            

            uint imageCount = capabilities.minImageCount + 1;

            if (capabilities.maxImageCount != 0 &&
                imageCount > capabilities.maxImageCount)
            {
                imageCount = capabilities.maxImageCount;
            }

            var createInfo = new VkSwapchainCreateInfoKHR
            {
                surface = device.VkSurfaceKHR,
                minImageCount = imageCount,
                imageFormat = SurfaceFormat.format,
                imageColorSpace = SurfaceFormat.colorSpace,
                imageExtent = Extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst,
                imageSharingMode = VkSharingMode.Exclusive,
                preTransform = capabilities.currentTransform,
                compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque,
                presentMode = presentMode,
                clipped = true,
                oldSwapchain = default
            };

            Check(
                device.VkDeviceApi.vkCreateSwapchainKHR(
                    &createInfo,
                    null,
                    out _swapchain),
                "Failed to create swapchain.");

            GetImages();
            CreateImageViews();
        }

        private void GetImages()
        {
            var device = RenderDevice!;

            uint imageCount = 0;

            Check(
                device.VkDeviceApi.vkGetSwapchainImagesKHR(
                    _swapchain,
                    &imageCount,
                    null),
                "Failed to query swapchain images.");

            var images = new VkImage[imageCount];

            fixed (VkImage* imagesPtr = images)
            {
                Check(
                    device.VkDeviceApi.vkGetSwapchainImagesKHR(
                        _swapchain,
                        &imageCount,
                        imagesPtr),
                    "Failed to retrieve swapchain images.");
            }

            _images.Clear();
            _images.AddRange(images);
        }

        private void CreateImageViews()
        {
            var device = RenderDevice!;

            foreach (var image in _images)
            {
                var createInfo = new VkImageViewCreateInfo
                {
                    image = image,
                    viewType = VkImageViewType.Image2D,
                    format = SurfaceFormat.format,
                    components = new VkComponentMapping
                    {
                        r = VkComponentSwizzle.Identity,
                        g = VkComponentSwizzle.Identity,
                        b = VkComponentSwizzle.Identity,
                        a = VkComponentSwizzle.Identity
                    },
                    subresourceRange = new VkImageSubresourceRange
                    {
                        aspectMask = VkImageAspectFlags.Color,
                        baseMipLevel = 0,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1
                    }
                };

                Check(
                    device.VkDeviceApi.vkCreateImageView(
                        &createInfo,
                        null,
                        out var imageView),
                    "Failed to create swapchain image view.");

                _imageViews.Add(imageView);
            }
        }


        //H7per: TODO: This is a bit hacky. I'd like us to have more control over surface format (HDR?)
        private static VkSurfaceFormatKHR ChooseSurfaceFormat(
            VkSurfaceFormatKHR[] formats)
        {
            foreach (var format in formats)
            {
                if (format.format == VkFormat.B8G8R8A8Unorm &&
                    format.colorSpace == VkColorSpaceKHR.SrgbNonLinear)
                {
                    return format;
                }
            }

            return formats[0];
        }

        //H7per: TODO: Similar to ChooseSurfaceFormat, this lacks control.
        //Fifo is fine for testing for now, but we'd only want that with Vsync on.
        private static VkPresentModeKHR ChoosePresentMode(
            VkPresentModeKHR[] modes)
        {
            foreach (var mode in modes)
            {
                if (mode == VkPresentModeKHR.Mailbox)
                    return mode;
            }

            return VkPresentModeKHR.Fifo;
        }

        private static VkExtent2D ChooseExtent(
            VkSurfaceCapabilitiesKHR capabilities,
            uint width,
            uint height)
        {
            if (capabilities.currentExtent.width != uint.MaxValue)
                return capabilities.currentExtent;

            return new VkExtent2D
            {
                width = Math.Clamp(
                    width,
                    capabilities.minImageExtent.width,
                    capabilities.maxImageExtent.width),

                height = Math.Clamp(
                    height,
                    capabilities.minImageExtent.height,
                    capabilities.maxImageExtent.height)
            };
        }

        public uint AcquireNextImage(VkSemaphore imageAvailableSemaphore)
        {
            var device = RenderDevice
                ?? throw new InvalidOperationException(
                    "S2vDevice has not been initialized.");

            Check(
                device.VkDeviceApi.vkAcquireNextImageKHR(
                    _swapchain,
                    ulong.MaxValue,
                    imageAvailableSemaphore,
                    default,
                    out uint imageIndex),
                "Failed to acquire swapchain image.");
            _currentImageIndex = imageIndex;
            return imageIndex;
        }


        public void Present(VkSemaphore renderFinishedSemaphore)
        {
            fixed (VkSwapchainKHR* pSwapchain = &_swapchain)
            fixed (uint* pImageIndex = &_currentImageIndex)
            {
                VkPresentInfoKHR presentInfo = new VkPresentInfoKHR
                {
                    waitSemaphoreCount = 1,
                    pWaitSemaphores = &renderFinishedSemaphore,
                    swapchainCount = 1,
                    pSwapchains = pSwapchain,
                    pImageIndices = pImageIndex
                };

                RenderDevice.VkDeviceApi.vkQueuePresentKHR(RenderDevice.GraphicsQueue, &presentInfo);
            }
        }

        public void Dispose()
        {
            var device = RenderDevice;

            if (device == null)
                return;

            foreach (var imageView in _imageViews)
            {
                device.VkDeviceApi.vkDestroyImageView(
                    imageView,
                    null);
            }

            _imageViews.Clear();
            _images.Clear();

            if (_swapchain.Handle != 0)
            {
                device.VkDeviceApi.vkDestroySwapchainKHR(
                    _swapchain,
                    null);

                _swapchain = default;
            }
        }

        //H7per: TODO: This should not be a member function of Swapchain.
        private static void Check(
            VkResult result,
            string message)
        {
            if (result != VkResult.Success)
                throw new Exception(
                    $"{message} Vulkan returned {result}.");
        }
    }
}