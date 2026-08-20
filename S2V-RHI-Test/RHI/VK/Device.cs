global using static S2vDevice;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Renderer;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using SilkVk = Silk.NET.Vulkan;


public static class S2vDevice
{
    public static S2V_RHI_Test.RHI.VK.Device? Device { get; private set; }

    public static void createS2vDevice(IWindow window)
    {
        if (Device == null)
        {
            Device = new S2V_RHI_Test.RHI.VK.Device(window);
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
        SilkVk.Instance VkInstance;
        SilkVk.Device VkDevice;
        SilkVk.PhysicalDevice VkPhysicalDevice;
        SilkVk.SurfaceKHR VkSurfaceKHR;

        SilkVk.Queue GraphicsQueue;
        SilkVk.Queue TransferQueue;

        unsafe public Device(IWindow window)
        {

            if (window.VkSurface is null)
                throw new Exception("windowing platform doesn't support Vulkan surfaces");

            var appInfo = new SilkVk.ApplicationInfo
            {
                SType = SilkVk.StructureType.ApplicationInfo,
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("S2V"),
                ApplicationVersion = SilkVk.Vk.MakeVersion(1, 0, 0),
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi("S2VEngine"),
                EngineVersion = SilkVk.Vk.MakeVersion(20, 0, 0),
                ApiVersion = SilkVk.Vk.Version13
            };

            var glfwExtensions = window.VkSurface!.GetRequiredExtensions(out uint extensionCount);

            var extensionNames = Silk.NET.Core.Native.SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)extensionCount);
            foreach (var name in extensionNames)
                Console.WriteLine($"Enabling instance extension: {name}");


            var validationLayers = new[] { "VK_LAYER_KHRONOS_validation" };
            var layerNamesPtr = (byte**)Silk.NET.Core.Native.SilkMarshal.StringArrayToPtr(validationLayers);

            var instanceCreateInfo = new SilkVk.InstanceCreateInfo
            {
                SType = SilkVk.StructureType.InstanceCreateInfo,
                EnabledLayerCount = 1,
                PpEnabledLayerNames = layerNamesPtr,
                EnabledExtensionCount = extensionCount,
                PpEnabledExtensionNames = glfwExtensions,
                PApplicationInfo = &appInfo
            };

            SilkVk.Instance instance;
            if (vk.CreateInstance(&instanceCreateInfo, null, &instance) != SilkVk.Result.Success)
                throw new Exception("failed to create instance");

            VkInstance = instance;

            uint deviceCount = 0;
            vk.EnumeratePhysicalDevices(VkInstance, &deviceCount, null);
            if (deviceCount == 0) throw new Exception("no GPUs with SilkVk support");


            var devices = new SilkVk.PhysicalDevice[deviceCount];
            fixed (SilkVk.PhysicalDevice* devicesPtr = devices)
            {
                vk.EnumeratePhysicalDevices(VkInstance, &deviceCount, devicesPtr);
            }

            //just take the zeroth device for now!
            VkPhysicalDevice = devices[0];

            vk.GetPhysicalDeviceProperties(VkPhysicalDevice, out var properties);

            Console.WriteLine("Picked GPU: " + Marshal.PtrToStringAnsi((nint)properties.DeviceName));

            var features13 = new SilkVk.PhysicalDeviceVulkan13Features
            {
                SType = SilkVk.StructureType.PhysicalDeviceVulkan13Features,
                DynamicRendering = true,
                Synchronization2 = true
            };

            Silk.NET.Vulkan.Extensions.KHR.KhrSurface khrSurface;
            if (!vk.TryGetInstanceExtension(VkInstance, out khrSurface))
                throw new Exception("VK_KHR_surface extension not found");

            VkSurfaceKHR = window.VkSurface
            .Create<SilkVk.AllocationCallbacks>(instance.ToHandle(), null)
            .ToSurface();

            var queueFamilyIndices = FindQueueFamilies(VkPhysicalDevice, khrSurface, VkSurfaceKHR);

            var graphicsQueuePriority = 1.0f;
            var graphicsQueueCreateInfo = new SilkVk.DeviceQueueCreateInfo
            {
                SType = SilkVk.StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = queueFamilyIndices.GraphicsFamily!.Value,
                QueueCount = 1,
                PQueuePriorities = &graphicsQueuePriority
            };

            var transferQueuePriority = 0.1f;

            var transferQueueCreateInfo = new SilkVk.DeviceQueueCreateInfo
            {
                SType = SilkVk.StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = queueFamilyIndices.TransferFamily!.Value,
                QueueCount = 1,
                PQueuePriorities = &transferQueuePriority
            };

            var deviceFeatures = new PhysicalDeviceFeatures();


            var queueCreateInfos = stackalloc SilkVk.DeviceQueueCreateInfo[2] { graphicsQueueCreateInfo, transferQueueCreateInfo };

            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 2,
                PQueueCreateInfos = queueCreateInfos,
                PEnabledFeatures = &deviceFeatures,
                EnabledExtensionCount = 0,
                PpEnabledExtensionNames = null
            };
            SilkVk.Device createdDevice;
            var result = vk.CreateDevice(VkPhysicalDevice, &deviceCreateInfo, null, &createdDevice);

            if (result != SilkVk.Result.Success)
                throw new Exception($"failed to create logical device: {result}");

            VkDevice = createdDevice;

            vk.GetDeviceQueue(VkDevice, queueFamilyIndices.GraphicsFamily!.Value, 0, out GraphicsQueue);

            vk.GetDeviceQueue(VkDevice, queueFamilyIndices.TransferFamily!.Value, 0, out TransferQueue);

        }

        unsafe QueueFamilyIndices FindQueueFamilies(
    SilkVk.PhysicalDevice physicalDevice,
    SilkVk.Extensions.KHR.KhrSurface khrSurface,
    SilkVk.SurfaceKHR surface)
        {
            var indices = new QueueFamilyIndices();

            uint count = 0;
            vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, null);

            var families = new SilkVk.QueueFamilyProperties[count];
            fixed (SilkVk.QueueFamilyProperties* famPtr = families)
            {
                vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, famPtr);
            }

            for (uint i = 0; i < count; i++)
            {
                var flags = families[i].QueueFlags;
                khrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, surface, out var presentSupport);
                if (flags.HasFlag(SilkVk.QueueFlags.GraphicsBit) && presentSupport)
                {
                    indices.GraphicsFamily = i;
                }

                // prefer a DEDICATED transfer family: has transfer bit, but NOT graphics
                // (graphics/compute queues always implicitly support transfer anyway)
                if (flags.HasFlag(SilkVk.QueueFlags.TransferBit) &&
                    !flags.HasFlag(SilkVk.QueueFlags.GraphicsBit))
                {
                    indices.TransferFamily = i;
                }


                if (!indices.GraphicsFamily.HasValue)
                {
                    throw new Exception("This Device does not have a queue family supporting graphics AND present");
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
    }
}
