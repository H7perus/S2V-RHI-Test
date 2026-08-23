using S2V_RHI_Test.RHI.VK;
using SDL;
using System.Runtime.InteropServices;
using System.Xml.Linq;
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
            800, 600,
            SDL_WindowFlags.SDL_WINDOW_VULKAN | SDL_WindowFlags.SDL_WINDOW_RESIZABLE
        );

        if (window == null)
        {
            Console.WriteLine($"SDL_CreateWindow failed: {SDL_GetError()}");
            return;
        }



        //From here on, we can use the device!
        createS2vDevice(window);

        var swapchain = new Swapchain(800, 600);

        bool running = true;

        var cmd = new CommandList();

        var pipeline = new Pipeline();


        var imageAvailableSemaphore = RenderDevice!.CreateSemaphore();

        var fifFreedFence = RenderDevice.CreateFence(true);

        while (running)
        {
            SDL_Event e;
            while (SDL_PollEvent(&e))
            {
                if (e.Type == SDL_EventType.SDL_EVENT_QUIT)
                    running = false;
            }

            RenderDevice.WaitForFences(fifFreedFence);
            RenderDevice.ResetFences(fifFreedFence);

            int imageIndex = swapchain.AcquireNextImage(imageAvailableSemaphore);
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
                image = swapchain.Images[imageIndex],
                subresourceRange = new VkImageSubresourceRange { aspectMask = VkImageAspectFlags.Color, baseMipLevel = 0, levelCount = 1, baseArrayLayer = 0, layerCount = 1 }
            };


            barrier[0].oldLayout = swapchain.ImageLayouts[imageIndex];

            barrier[0].newLayout = VkImageLayout.ColorAttachmentOptimal;

            cmd.PipelineBarrier(barrier);


            cmd.ClearSwapchainImage(
                swapchain.Images[imageIndex],
                new VkClearColorValue(0.1f, 0.2f, 0.4f, 1.0f));


            

            VkRenderingAttachmentInfo colorAttachmentInfo = new VkRenderingAttachmentInfo
            {
                imageView = swapchain.ImageViews[imageIndex],
                imageLayout = VkImageLayout.ColorAttachmentOptimal,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new VkClearValue
                {
                    color = new VkClearColorValue(0.2f, 0.1f, 0.4f, 1.0f)
                }
            };

            VkRenderingInfo renderingInfo = new VkRenderingInfo
            {
                sType = VkStructureType.RenderingInfo,
                renderArea = new VkRect2D
                {
                    offset = new(),
                    extent = swapchain.Extent
                },
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colorAttachmentInfo
            };

            RenderDevice!.VkDeviceApi.vkCmdBeginRendering(cmd.Handle, &renderingInfo);

            VkViewport viewport = new() { x = 0, y = 0, width = swapchain.Extent.width, height = swapchain.Extent.height, minDepth = 0.0f, maxDepth = 1.0f };
            VkRect2D scissor = new() { extent = swapchain.Extent, offset = new() };
            
            
            cmd.SetViewport(viewport);
            cmd.SetScissor(scissor);

            cmd.BindGraphicsPipeline(pipeline);

            cmd.Draw(3, 1, 0, 0);

            cmd.EndRendering();

            barrier[0].oldLayout = swapchain.ImageLayouts[imageIndex];
            barrier[0].newLayout = VkImageLayout.PresentSrcKHR;

            cmd.PipelineBarrier(barrier);

            cmd.End();

            var renderFinishedSemaphore = swapchain.WriteToImageFinishedSemaphores[imageIndex];

            RenderDevice.SubmitGraphics(cmd, imageAvailableSemaphore, renderFinishedSemaphore, fifFreedFence);
            swapchain.Present(renderFinishedSemaphore);
        }

        return;
    }
}