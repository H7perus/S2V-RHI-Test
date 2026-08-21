using S2V_RHI_Test.RHI.VK;
using SDL;
using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static SDL.SDL3;
using static System.Net.Mime.MediaTypeNames;

namespace HelloTriangle;

unsafe public static class Program
{
    public static void Main()
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            Console.WriteLine($"SDL_Init failed: {SDL_GetError()}");
            return;
        }

        SDL_Window* window = SDL_CreateWindow(
            "S2V-RHI-Test",
            1280, 720,
            SDL_WindowFlags.SDL_WINDOW_VULKAN | SDL_WindowFlags.SDL_WINDOW_RESIZABLE
        );

        if (window == null)
        {
            Console.WriteLine($"SDL_CreateWindow failed: {SDL_GetError()}");
            return;
        }



        //From here on, we can use the device!
        createS2vDevice(window);

        var swapchain = new Swapchain(1280, 720);

        bool running = true;

        var cmd = new CommandList();


        VkSemaphoreCreateInfo semaphoreCreateInfo = new VkSemaphoreCreateInfo
        {
        };

        RenderDevice.VkDeviceApi.vkCreateSemaphore(semaphoreCreateInfo, out var imageAvailableSemaphore);
        RenderDevice.VkDeviceApi.vkCreateSemaphore(semaphoreCreateInfo, out var renderFinishedSemaphore);

        VkFenceCreateInfo fenceCreateInfo = new VkFenceCreateInfo
        {
            flags = VkFenceCreateFlags.Signaled
        };

        RenderDevice.VkDeviceApi.vkCreateFence(fenceCreateInfo, out var fifFreedFence);

        uint frameCount = 0;


        while (running)
        {
            SDL_Event e;
            while (SDL_PollEvent(&e))
            {
                if (e.Type == SDL_EventType.SDL_EVENT_QUIT)
                    running = false;
            }

            RenderDevice.VkDeviceApi.vkWaitForFences(fifFreedFence, true, uint.MaxValue);

            RenderDevice.VkDeviceApi.vkResetFences(fifFreedFence);

            uint imageIndex = swapchain.AcquireNextImage(imageAvailableSemaphore);
            //cmd.Reset();
            cmd.Begin();


            VkImageMemoryBarrier2[] barrier = new VkImageMemoryBarrier2[1];

            barrier[0] = new VkImageMemoryBarrier2
            {
                srcStageMask = VkPipelineStageFlags2.TopOfPipe,
                srcAccessMask = VkAccessFlags2.None,
                dstStageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                dstAccessMask = VkAccessFlags2.ColorAttachmentWrite,
                oldLayout = VkImageLayout.Undefined,
                newLayout = VkImageLayout.PresentSrcKHR,
                image = swapchain.Images[(int)imageIndex],
                subresourceRange = new VkImageSubresourceRange { aspectMask = VkImageAspectFlags.Color, baseMipLevel = 0, levelCount = 1, baseArrayLayer = 0, layerCount = 1 }
            }; ;


            barrier[0].oldLayout = VkImageLayout.PresentSrcKHR;

            if (frameCount++ < 3)
            {
                barrier[0].oldLayout = VkImageLayout.Undefined;
            }

            barrier[0].newLayout = VkImageLayout.ColorAttachmentOptimal;

            cmd.PipelineBarrier(barrier);


            cmd.ClearSwapchainImage(
                swapchain.Images[(int)imageIndex],
                new VkClearColorValue(0.1f, 0.2f, 0.4f, 1.0f));


            barrier[0].oldLayout = VkImageLayout.ColorAttachmentOptimal;
            barrier[0].newLayout = VkImageLayout.PresentSrcKHR;

            cmd.PipelineBarrier(barrier);

            cmd.End();

            RenderDevice.SubmitGraphics(cmd, imageAvailableSemaphore, renderFinishedSemaphore, fifFreedFence);
            swapchain.Present(renderFinishedSemaphore);
        }

        return;
    }
}