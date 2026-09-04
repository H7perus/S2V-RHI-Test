global using static S2vDevice;

using S2V_RHI_Test.RHI;
using S2V_RHI_Test.RHI.ShaderCompile;
using SDL;
using Vortice.Vulkan;
using static SDL.SDL3;
using Buffer = S2V_RHI_Test.RHI.Buffer;

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
            SDL_WindowFlags.SDL_WINDOW_VULKAN
        );

        if (window == null)
        {
            Console.WriteLine($"SDL_CreateWindow failed: {SDL_GetError()}");
            return;
        }

        createS2vDevice(window);

        SlangShaderCompiler slangCompiler = new();

        var module = slangCompiler.LoadShaderModule("Shaders/triangle.slang");

        SpecialisedShader specShader = slangCompiler.SpecialiseAndCompile(module);

        var swapchain = new Swapchain(800, 600);

        bool running = true;

        var cmd = new CommandList(RenderDevice!.QueueFamilyIndices.GraphicsFamily!.Value);
        var transferCmd = new CommandList(RenderDevice!.QueueFamilyIndices.TransferFamily!.Value);

        var pipeline = new PipelineGraphics(specShader, VkFormat.B8G8R8A8Unorm);



        var vBuffer = new Buffer(1000, VkBufferUsageFlags.VertexBuffer, VmaMemoryUsage.GpuToCpu);
        var vAttribBuffer = new Buffer(1000, VkBufferUsageFlags.VertexBuffer, VmaMemoryUsage.GpuToCpu);

        var iBuffer = new Buffer(1000, VkBufferUsageFlags.IndexBuffer, VmaMemoryUsage.GpuToCpu);

        float* vBufferMap = (float*)vBuffer.Map();

        float[] positions =
            {
                0.0f, -0.5f, 0.0f,
                0.5f, 0.5f, 0.0f,
                -0.5f, 0.5f, 0.0f
            };

        for (int i = 0; i < 9; i++)
        {
            vBufferMap[i] = positions[i];
        }
        vBuffer.Unmap();

        uint* indexPtr = (uint*)iBuffer.Map();

        indexPtr[0] = 0;
        indexPtr[1] = 1;
        indexPtr[2] = 2;
        iBuffer.Unmap();

        float* bufferMap = (float*)vAttribBuffer.Map();

        float[] colors =
            {
                1.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 1.0f,
            };

        for (int i = 0; i < 9; i++)
        {
            bufferMap[i] = colors[i];
        }


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

            cmd.ColorImageTransition(swapchain.Images[imageIndex], ref swapchain.ImageLayouts[imageIndex], VkImageLayout.ColorAttachmentOptimal);


            cmd.ClearSwapchainImage(
                swapchain.Images[imageIndex],
                swapchain.ImageLayouts[imageIndex],
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
                renderArea = new VkRect2D
                {
                    extent = swapchain.Extent
                },
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colorAttachmentInfo
            };

            cmd.BeginRendering(renderingInfo);

            VkViewport viewport = new() { x = 0, y = 0, width = swapchain.Extent.width, height = swapchain.Extent.height, minDepth = 0.0f, maxDepth = 1.0f };
            VkRect2D scissor = new() { extent = swapchain.Extent, offset = new() };


            cmd.SetViewport(viewport);
            cmd.SetScissor(scissor);

            cmd.BindGraphicsPipeline(pipeline);

            cmd.BindVertexBuffer(vBuffer, binding: 0);
            cmd.BindVertexBuffer(vAttribBuffer, binding: 1);

            cmd.BindIndexBuffer(iBuffer);

            cmd.DrawIndexed(3, 1, 0, 0, 0);

            cmd.EndRendering();

            cmd.ColorImageTransition(swapchain.Images[imageIndex], ref swapchain.ImageLayouts[imageIndex], VkImageLayout.PresentSrcKHR);

            cmd.End();

            var renderFinishedSemaphore = swapchain.WriteToImageFinishedSemaphores[imageIndex];

            RenderDevice.SubmitGraphics(cmd, imageAvailableSemaphore, renderFinishedSemaphore, fifFreedFence);
            swapchain.Present(renderFinishedSemaphore);
        }

        return;
    }
}