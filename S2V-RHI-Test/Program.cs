using S2V_RHI_Test.RHI;
using S2V_RHI_Test.RHI.ShaderCompile;
using SDL;
using System.Diagnostics;
using System.Numerics;

using System.Runtime.InteropServices;
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
            SDL_WindowFlags.SDL_WINDOW_VULKAN | SDL_WindowFlags.SDL_WINDOW_RESIZABLE
        );

        if (window == null)
        {
            Console.WriteLine($"SDL_CreateWindow failed: {SDL_GetError()}");
            return;
        }



        //From here on, we can use the device!
        createS2vDevice(window);





        SlangShaderCompiler slangCompiler = new();

        var rootPath = "../../../";

        var shaderString = File.ReadAllText(rootPath + "Shaders/testShaderDescriptorHandle.slang");

        var spirv = slangCompiler.Compile(shaderString);

        var swapchain = new Swapchain(800, 600);

        bool running = true;

        var cmd = new CommandList();

        var pipeline = new PipelineGraphics(spirv);



        var buffer = new Buffer(1000, VkBufferUsageFlags.UniformBuffer, VmaMemoryUsage.GpuToCpu);

        var vBuffer = new Buffer(1000, VkBufferUsageFlags.VertexBuffer, VmaMemoryUsage.GpuToCpu);
        var iBuffer = new Buffer(1000, VkBufferUsageFlags.IndexBuffer, VmaMemoryUsage.GpuToCpu);

        Vector3* vBufferMap = (Vector3*)vBuffer.Map();

        Vector3[] positions = new Vector3[4]
            {
                new Vector3(0.5f, -0.5f, 0.0f),
                new Vector3(0.5f, 0.5f, 0.0f),
                new Vector3(-0.5f, 0.5f, 0.0f),
                new Vector3(-0.5f, -0.5f, 0.0f)
            };

        for (int i = 0; i < 4; i++)
        {
            vBufferMap[i] = positions[i];
        }
        vBuffer.Unmap();

        uint* indexPtr = (uint*)iBuffer.Map();

        



        indexPtr[0] = 0;
        indexPtr[1] = 1;
        indexPtr[2] = 2;
        indexPtr[3] = 3;
        indexPtr[4] = 2;
        indexPtr[5] = 0;
        iBuffer.Unmap();

        float* bufferMap = (float*)buffer.Map();

        float[] colors = new float[16]
            {
                1.0f, 0.0f, 0.5f, 0.0f,
                1.0f, 0.5f, 0.0f, 0.0f,
                1.0f, 0.5f, 0.0f, 0.0f,
                1.0f, 0.0f, 0.5f, 0.0f,
            };

        for (int i = 0; i < 16; i++)
        {
            bufferMap[i] = colors[i];
        }
        

        Matrix4x4 CreatePerspectiveFieldOfView_ReverseZ(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance = float.PositiveInfinity)
        {
            var height = 1.0f / MathF.Tan(fieldOfView * 0.5f);
            var width = height / aspectRatio;

            var m33 = 0.0f;
            var m43 = nearPlaneDistance;

            if (float.IsFinite(farPlaneDistance))
            {
                var range = farPlaneDistance - nearPlaneDistance;

                m33 = nearPlaneDistance / range;
                m43 = nearPlaneDistance * farPlaneDistance / range;
            }

            return new Matrix4x4
            {
                M11 = width,
                M22 = height,
                M33 = m33,
                M34 = -1.0f,
                M43 = m43
            };
        }


        var imageAvailableSemaphore = RenderDevice!.CreateSemaphore();

        var fifFreedFence = RenderDevice.CreateFence(true);
        var stopwatch = Stopwatch.StartNew();

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

            Matrix4x4 perspectiveMat = Matrix4x4.CreatePerspectiveFieldOfView(1.6f, 4f / 3f, 0.1f, 10f);

            Matrix4x4 viewMat = Matrix4x4.CreateFromYawPitchRoll(0.0f, 0f, 0.75f) * Matrix4x4.CreateFromYawPitchRoll((float)stopwatch.Elapsed.TotalSeconds, 0, 0) * Matrix4x4.CreateTranslation(new Vector3(0, 0, -4f));

            *((Matrix4x4*)(bufferMap + 16)) = viewMat * perspectiveMat; // perspectiveMat * viewMat; // perspectiveMat;


            int imageIndex = swapchain.AcquireNextImage(imageAvailableSemaphore);
            cmd.Begin();


            uint[] descriptorHandleValue = { 0, 0 };

            cmd.PushConstants(buffer.DescriptorHandle);

            

            cmd.ColorImageTransition(swapchain.Images[imageIndex], ref swapchain.ImageLayouts[imageIndex], VkImageLayout.ColorAttachmentOptimal);


            cmd.ClearSwapchainImage(
                swapchain.Images[imageIndex],
                ref swapchain.ImageLayouts[imageIndex],
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

            cmd.BeginRendering(renderingInfo);

            VkViewport viewport = new() { x = 0, y = 0, width = swapchain.Extent.width, height = swapchain.Extent.height, minDepth = 0.0f, maxDepth = 1.0f };
            VkRect2D scissor = new() { extent = swapchain.Extent, offset = new() };


            cmd.SetViewport(viewport);
            cmd.SetScissor(scissor);

            cmd.BindGraphicsPipeline(pipeline);

            cmd.BindVertexBuffer(vBuffer, binding: 0);

            cmd.BindIndexBuffer(iBuffer);

            cmd.DrawIndexed(6, 1, 0, 0, 0);

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