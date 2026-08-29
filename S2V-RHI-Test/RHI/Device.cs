global using static S2vDevice;
using S2V_RHI_Test.RHI;
using SDL;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Vulkan;
using static SDL.SDL3;
using static Vortice.Vulkan.Vma;
using static Vortice.Vulkan.Vulkan;

public static class S2vDevice
{
    public static Device? RenderDevice { get; private set; }

    unsafe public static void createS2vDevice(SDL_Window* window)
    {
        if (RenderDevice == null)
        {
            RenderDevice = new S2V_RHI_Test.RHI.Device(window);
        }
    }
}


namespace S2V_RHI_Test.RHI
{
    public struct QueueFamilyIndices
    {
        public uint? GraphicsFamily;
        public uint? PresentFamily;
        public uint? TransferFamily;

        public readonly bool IsComplete =>
            GraphicsFamily.HasValue && PresentFamily.HasValue && TransferFamily.HasValue;
    };


    enum BindlessBindingIndex
    {
        CombinedImageSampler = 1,
        UniformBuffer = 6,
        StorageBuffer = 7,
    }


    public class Device
    {

        internal VkInstanceApi VkInstanceApi;
        internal VkDeviceApi VkDeviceApi;
        internal VkPhysicalDevice VkPhysicalDevice;
        internal VkSurfaceKHR VkSurfaceKHR;


        private class BindlessManagerType
        {
            private sealed class BindlessSlotAllocator
            {
                private readonly Stack<uint> FreeIndices = new();
                private uint NextIndex = 0;

                private uint Capacity;

                public BindlessSlotAllocator(uint capacity)
                {
                    Capacity = capacity;
                }
                
                public uint Allocate()
                {
                    if (FreeIndices.TryPop(out uint index))
                    {
                        return index;
                    }

                    if (NextIndex == Capacity)
                    {
                        throw new InvalidOperationException("Ran out of bindless slots for the descriptor type");
                    }

                    index = NextIndex++;
                    return index;
                }

                public void Free(uint index)
                {
                    FreeIndices.Push(index);
                }
            }
            private readonly BindlessSlotAllocator[] Allocators;

            public BindlessManagerType()
            {
                Allocators = new BindlessSlotAllocator[Enum.GetValues<BindlessBindingIndex>().Length];
                for (int i = 0; i < Allocators.Length; i++)
                    Allocators[i] = new BindlessSlotAllocator(1000);
            }

            public uint GetFreeBindlessIndex(BindlessBindingIndex bindingIndex)
            {
                var allocatorIndex = Array.IndexOf(Enum.GetValues<BindlessBindingIndex>(), bindingIndex);

                return Allocators[allocatorIndex].Allocate();
            }

            public void FreeBindlessIndex(BindlessBindingIndex bindingIndex, uint index)
            {
                var allocatorIndex = Array.IndexOf(Enum.GetValues<BindlessBindingIndex>(), bindingIndex);

                Allocators[allocatorIndex].Free(index);
            }
        }
        private readonly BindlessManagerType BindlessManager = new();
        internal VkDescriptorSetLayout SharedBindlessDescriptorSetLayout;
        internal VkDescriptorSet SharedBindlessDescriptorSet;
        internal VkPipelineLayout SharedPipelineLayout;
        internal VkDescriptorPool SharedDescriptorPool;

        internal QueueFamilyIndices QueueFamilyIndices;
        private VkQueue _graphicsQueue;
        internal VkQueue GraphicsQueue => _graphicsQueue;
        private VkQueue _transferQueue;
        internal VkQueue TransferQueue => _transferQueue;
        internal VmaAllocator VmaAllocator;

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


            var features12 = new VkPhysicalDeviceVulkan12Features
            {
                descriptorBindingSampledImageUpdateAfterBind = true,
                descriptorBindingUniformBufferUpdateAfterBind = true,
                descriptorBindingStorageBufferUpdateAfterBind = true,
                descriptorBindingPartiallyBound = true,
                descriptorBindingVariableDescriptorCount = true,
                
                runtimeDescriptorArray = true
            };

            var features13 = new VkPhysicalDeviceVulkan13Features
            {
                pNext = &features12,
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


            VmaAllocatorCreateInfo allocatorCreateInfo = new()
            {
                instance = VkInstanceApi.Instance,
                device = VkDeviceApi.Device,
                physicalDevice = VkPhysicalDevice,
                vulkanApiVersion = appInfo.apiVersion,
            };

            vmaCreateAllocator(&allocatorCreateInfo, out var allocator);
            VmaAllocator = allocator;


            CreateSharedBindlessDescriptorSetLayout();
            CreateSharedPipelineLayout();
            CreateSharedDescriptorPool();
            CreateSharedBindlessDescriptorSet();
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

        internal unsafe uint GetBindlessSlot(VkDescriptorType descriptorType, VkBuffer bufferHandle)
        {
            var bindlessIndex = BindlessManager.GetFreeBindlessIndex((BindlessBindingIndex)descriptorType);

            VkDescriptorBufferInfo bufferInfo = new()
            {
                buffer = bufferHandle,
                offset = 0,
                range = Vulkan.VK_WHOLE_SIZE
            };

            VkWriteDescriptorSet write = new()
            {
                dstSet = RenderDevice!.SharedBindlessDescriptorSet,
                dstBinding = 6,
                dstArrayElement = bindlessIndex,
                descriptorCount = 1,
                descriptorType = VkDescriptorType.UniformBuffer,
                pBufferInfo = &bufferInfo
            };

            RenderDevice!.VkDeviceApi.vkUpdateDescriptorSets(1, &write, 0, null);

            return bindlessIndex;
        }

        internal void FreeBindlessindex(VkDescriptorType descriptorType, uint index)
        {
            BindlessManager.FreeBindlessIndex((BindlessBindingIndex)descriptorType, index);
        }

        private unsafe void CreateSharedBindlessDescriptorSetLayout()
        {

            var bindings = Enum.GetValues<BindlessBindingIndex>();

            var descriptorSetLayoutBindings = new VkDescriptorSetLayoutBinding[bindings.Length];

            for (var i = 0; i < bindings.Length; i++)
            {
                descriptorSetLayoutBindings[i].binding = (uint)bindings[i];
                //Looks like nonsense, but the bindings match VkDescriptorType's values. I believe this is by design on slangs side.
                descriptorSetLayoutBindings[i].descriptorType = (VkDescriptorType)bindings[i];
                descriptorSetLayoutBindings[i].stageFlags = VkShaderStageFlags.All;
                //H7per: TODO: We probably want to make this more fine grained. We won't need as many buffers as we do textures, for instance.
                descriptorSetLayoutBindings[i].descriptorCount = 1000;
            }

            //These flags are static for us. Just annoying we have to have them N times.
            VkDescriptorBindingFlags[] bindingFlags = Enumerable.Repeat(VkDescriptorBindingFlags.PartiallyBound | VkDescriptorBindingFlags.UpdateAfterBind, bindings.Length).ToArray();


            fixed (VkDescriptorBindingFlags* pBindingFlags = bindingFlags)
            fixed (VkDescriptorSetLayoutBinding* pDescriptorSetLayoutBindings = descriptorSetLayoutBindings)
            {
                VkDescriptorSetLayoutBindingFlagsCreateInfo bindingFlagsInfo = new()
                {
                    bindingCount = (uint)bindings.Length,
                    pBindingFlags = pBindingFlags
                }; 

                VkDescriptorSetLayoutCreateInfo setLayoutCreateInfo = new()
                {
                    flags = VkDescriptorSetLayoutCreateFlags.UpdateAfterBindPool,
                    pNext = &bindingFlagsInfo,
                    bindingCount = (uint)bindings.Length,
                    pBindings = pDescriptorSetLayoutBindings,
                };


                VkDeviceApi.vkCreateDescriptorSetLayout(setLayoutCreateInfo, out SharedBindlessDescriptorSetLayout);
            }
        }

        private unsafe void CreateSharedFixedDescriptorSetLayout()
        { }

        private unsafe void CreateSharedPipelineLayout()
        {

            //PLACEHOLDER
            VkDeviceApi.vkCreateDescriptorSetLayout(new VkDescriptorSetLayoutCreateInfo(), out var setLayout0);

            var sets = new VkDescriptorSetLayout[2]
                    {
                        setLayout0,
                        SharedBindlessDescriptorSetLayout,
                    };

            //Fixed push constant range for us. We push a DescriptorHandle and nothing more.
            VkPushConstantRange pushRange = new()
            {
                stageFlags = VkShaderStageFlags.All,
                size = 8
            };

            fixed (VkDescriptorSetLayout* pSets = sets)
            {
                VkPipelineLayoutCreateInfo layoutCreateInfo = new()
                {
                    setLayoutCount = 2,
                    pSetLayouts = pSets,
                    pushConstantRangeCount = 1,
                    pPushConstantRanges = &pushRange
                };
                VkDeviceApi.vkCreatePipelineLayout(layoutCreateInfo, out SharedPipelineLayout);
            }
        }

        private unsafe void CreateSharedDescriptorPool()
        {
            var bindings = Enum.GetValues<BindlessBindingIndex>();

            VkDescriptorPoolSize[] poolSizes = new VkDescriptorPoolSize[bindings.Length];

            for (var i = 0; i < bindings.Length; i++)
            {
                poolSizes[i].type = (VkDescriptorType)bindings[i];
                poolSizes[i].descriptorCount = 10000;
            }

            fixed (VkDescriptorPoolSize* pPoolSizes = poolSizes)
            fixed (VkDescriptorPool* pPool = &SharedDescriptorPool)
            {
                VkDescriptorPoolCreateInfo poolInfo = new()
                {
                    flags = VkDescriptorPoolCreateFlags.UpdateAfterBind,
                    //Change this if it turns out we want more (like for having the fixed set per render context and FiF)
                    maxSets = 2,
                    poolSizeCount = (uint)bindings.Length,
                    pPoolSizes = pPoolSizes
                };

                VkDeviceApi.vkCreateDescriptorPool(&poolInfo, null, pPool).CheckResult();
            }
        }

        private unsafe void CreateSharedBindlessDescriptorSet()
        {

            fixed (VkDescriptorSetLayout* pSet = &SharedBindlessDescriptorSetLayout)
            {
                VkDescriptorSetAllocateInfo allocInfo = new()
                {
                    descriptorPool = SharedDescriptorPool,
                    descriptorSetCount = 1,
                    pSetLayouts = pSet
                };

                VkDeviceApi.vkAllocateDescriptorSets(allocInfo, out SharedBindlessDescriptorSet).CheckResult();
            }
        }

    }
    
}
