global using static S2vDevice;

using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

using SDL;
using static SDL.SDL3;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;


public static class S2vDevice
{
    public static S2V_RHI_Test.RHI.VK.Device? RenderDevice { get; private set; }

    unsafe public static void createS2vDevice(SDL_Window* window)
    {
        if (RenderDevice == null)
        {
            RenderDevice = new S2V_RHI_Test.RHI.VK.Device(window);
        }
    }
}


namespace S2V_RHI_Test.RHI.VK
{
    public struct QueueFamilyIndices
    {
        public uint? GraphicsFamily;
        public uint? PresentFamily;
        public uint? TransferFamily;

        public readonly bool IsComplete =>
            GraphicsFamily.HasValue && PresentFamily.HasValue && TransferFamily.HasValue;
    };



    public class Device
    {
        internal VkInstanceApi VkInstanceApi;
        internal VkDeviceApi VkDeviceApi;
        internal VkPhysicalDevice VkPhysicalDevice;
        internal VkSurfaceKHR VkSurfaceKHR;

        internal QueueFamilyIndices QueueFamilyIndices;
        private VkQueue _graphicsQueue;
        internal VkQueue GraphicsQueue => _graphicsQueue;
        private VkQueue _transferQueue;
        internal VkQueue TransferQueue => _transferQueue;

        unsafe public Device(SDL_Window* window)
        {
            vkInitialize();

            VkUtf8String appName = "S2v"u8;
            VkUtf8String engineName = "S2vEngine"u8;

            var appInfo = new VkApplicationInfo
            {
                pApplicationName = appName,
                applicationVersion = new VkVersion(1, 0, 0),
                pEngineName = engineName,
                engineVersion = new VkVersion(20, 0, 0),
                apiVersion = VkVersion.Version_1_3
            };



            var layerNamesPtr = (byte*)Marshal.StringToHGlobalAnsi("VK_LAYER_KHRONOS_validation");

            string[] layerNames = new string[] { "VK_LAYER_KHRONOS_validation" };

            VkStringArray VkLayerNames = new VkStringArray(layerNames);

            uint sdlExtensionCount = 0;
            byte** sdlExtensions = SDL_Vulkan_GetInstanceExtensions(&sdlExtensionCount);

            var instanceCreateInfo = new VkInstanceCreateInfo
            {
                enabledLayerCount = VkLayerNames.Length,
                ppEnabledLayerNames = VkLayerNames,
                enabledExtensionCount = sdlExtensionCount,
                ppEnabledExtensionNames = sdlExtensions,
                pApplicationInfo = &appInfo
            };

            if (vkCreateInstance(&instanceCreateInfo, null, out VkInstance instance) != VkResult.Success)
                throw new Exception("failed to create instance");


            VkInstanceApi = new VkInstanceApi(instance);

            uint deviceCount = 0;

            VkInstanceApi.vkEnumeratePhysicalDevices(&deviceCount, null);

            if (deviceCount == 0) throw new Exception("no GPUs with SilkVk support");


            var devices = new VkPhysicalDevice[deviceCount];
            fixed (VkPhysicalDevice* devicesPtr = devices)
            {
                VkInstanceApi.vkEnumeratePhysicalDevices(&deviceCount, devicesPtr);
            }

            //just take the zeroth device for now!
            VkPhysicalDevice = devices[0];

            VkInstanceApi.vkGetPhysicalDeviceProperties(VkPhysicalDevice, out var properties);

            Console.WriteLine("Picked GPU: " + Marshal.PtrToStringAnsi((nint)properties.deviceName));

            var features13 = new VkPhysicalDeviceVulkan13Features
            {
                dynamicRendering = true,
                synchronization2 = true
            };

            VkSurfaceKHR_T* pSurface;

            //None of this makes sense. It uses pSurface to inform us of the surface...Except the pointer itself becomes the handle.
            //This is utter nonsense, as in C/Cpp, SDL asks for a pointer to a surface, not a pointer to a pointer.
            SDL_Vulkan_CreateSurface(window, (VkInstance_T*)(instance.Handle), null, &pSurface);

            VkSurfaceKHR = new VkSurfaceKHR((ulong)pSurface);

            QueueFamilyIndices = FindQueueFamilies(VkPhysicalDevice, VkSurfaceKHR);

            var graphicsQueuePriority = 1.0f;
            var graphicsQueueCreateInfo = new VkDeviceQueueCreateInfo
            {
                sType = VkStructureType.DeviceQueueCreateInfo,
                queueFamilyIndex = QueueFamilyIndices.GraphicsFamily!.Value,
                queueCount = 1,
                pQueuePriorities = &graphicsQueuePriority
            };

            var transferQueuePriority = 0.1f;

            var transferQueueCreateInfo = new VkDeviceQueueCreateInfo
            {
                sType = VkStructureType.DeviceQueueCreateInfo,
                queueFamilyIndex = QueueFamilyIndices.TransferFamily!.Value,
                queueCount = 1,
                pQueuePriorities = &transferQueuePriority
            };

            var queueCreateInfos = stackalloc VkDeviceQueueCreateInfo[2] { graphicsQueueCreateInfo, transferQueueCreateInfo };


            string[] extensions = new string[] { "VK_KHR_swapchain" };

            VkStringArray extensionsArray = new VkStringArray(extensions);

            var deviceCreateInfo = new VkDeviceCreateInfo
            {
                sType = VkStructureType.DeviceCreateInfo,
                queueCreateInfoCount = 2,
                pQueueCreateInfos = queueCreateInfos,

                //Sketch as hell
                pEnabledFeatures = null,
                enabledExtensionCount = extensionsArray.Length,
                ppEnabledExtensionNames = extensionsArray,
                pNext = &features13
            };
            VkDevice createdDevice;
            var result = VkInstanceApi.vkCreateDevice(VkPhysicalDevice, &deviceCreateInfo, null, &createdDevice);

            if (result != VkResult.Success)
                throw new Exception($"failed to create logical device: {result}");

            VkDeviceApi = new VkDeviceApi(VkInstanceApi, createdDevice);

            VkDeviceApi.vkGetDeviceQueue(QueueFamilyIndices.GraphicsFamily!.Value, 0, out _graphicsQueue);

            VkDeviceApi.vkGetDeviceQueue(QueueFamilyIndices.TransferFamily!.Value, 0, out _transferQueue);

        }

        unsafe QueueFamilyIndices FindQueueFamilies(
    VkPhysicalDevice physicalDevice,
    VkSurfaceKHR surface)
        {
            var indices = new QueueFamilyIndices();

            uint count = 0;
            VkInstanceApi.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, null);

            var families = new VkQueueFamilyProperties[count];
            fixed (VkQueueFamilyProperties* famPtr = families)
            {
                VkInstanceApi.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, famPtr);
            }

            for (uint i = 0; i < count; i++)
            {
                var flags = families[i].queueFlags;
                VkInstanceApi.vkGetPhysicalDeviceSurfaceSupportKHR(physicalDevice, i, surface, out var presentSupport);
                if (flags.HasFlag(VkQueueFlags.Graphics) && presentSupport)
                {
                    indices.GraphicsFamily = i;
                }

                // prefer a DEDICATED transfer family: has transfer bit, but NOT graphics
                // (graphics/compute queues always implicitly support transfer anyway)
                if (flags.HasFlag(VkQueueFlags.Transfer) &&
                    !flags.HasFlag(VkQueueFlags.Graphics))
                {
                    indices.TransferFamily = i;
                }


                if (!indices.GraphicsFamily.HasValue)
                {
                    throw new Exception("This RenderDevice does not have a queue family supporting graphics AND present");
                }

                if (indices.IsComplete)
                    break;
            }

            // fallback: no dedicated transfer family found, just reuse graphics
            // (graphics-capable queues are guaranteed to support transfer per spec)
            if (!indices.TransferFamily.HasValue && indices.GraphicsFamily.HasValue)
            {
                indices.TransferFamily = indices.GraphicsFamily;
            }

            return indices;
        }

        public VkSemaphore CreateSemaphore()
        {
            VkDeviceApi.vkCreateSemaphore(out var semaphore);
            return semaphore;
        }
        //public void CreateSemaphore(out VkSemaphore semaphore)
        //{
        //    VkDeviceApi.vkCreateSemaphore(out semaphore);
        //}
        public VkFence CreateFence(bool isSignaled = false)
        {
            VkFenceCreateInfo fenceCreateInfo = new() { flags = (VkFenceCreateFlags)Convert.ToInt32(isSignaled) };

            VkDeviceApi.vkCreateFence(fenceCreateInfo, out var fence);
            return fence;
        }
        public void WaitForFences(VkFence fence, bool waitForAll = true, ulong timeout = ulong.MaxValue)
        {
            VkDeviceApi.vkWaitForFences(fence, waitForAll, timeout);
        }

        public void WaitForFences(Span<VkFence> fence, bool waitForAll = true, ulong timeout = ulong.MaxValue)
        {
            VkDeviceApi.vkWaitForFences(fence, waitForAll, timeout);
        }
        public void ResetFences(VkFence fence)
        {
            VkDeviceApi.vkResetFences(fence);
        }
        public void ResetFences(Span<VkFence> fences)
        {
            VkDeviceApi.vkResetFences(fences);
        }
        unsafe public void SubmitGraphics(CommandList list, VkSemaphore imageAvailableSemaphore = new VkSemaphore(), VkSemaphore renderFinishedSemaphore = new VkSemaphore(), VkFence fifFreed = new VkFence())
        {
            VkCommandBufferSubmitInfo cmdBufferSubmitInfo = new VkCommandBufferSubmitInfo
            {
                commandBuffer = list.Handle
            };

            VkSemaphoreSubmitInfo imgAvailableSemaphoreInfo = new VkSemaphoreSubmitInfo
            {
                semaphore = imageAvailableSemaphore
            };

            VkSemaphoreSubmitInfo renderFinishedSemaphoreInfo = new VkSemaphoreSubmitInfo
            {
                semaphore = renderFinishedSemaphore
            };

            VkSubmitInfo2 submitInfo = new VkSubmitInfo2
            {
                waitSemaphoreInfoCount = (uint)Convert.ToInt32(!imageAvailableSemaphore.IsNull),
                pWaitSemaphoreInfos = &imgAvailableSemaphoreInfo,
                commandBufferInfoCount = 1,
                pCommandBufferInfos = &cmdBufferSubmitInfo,
                signalSemaphoreInfoCount = (uint)Convert.ToInt32(!renderFinishedSemaphore.IsNull),
                pSignalSemaphoreInfos = &renderFinishedSemaphoreInfo
            };

            VkDeviceApi.vkQueueSubmit2(_graphicsQueue, submitInfo, fifFreed);
        }
    }
}
