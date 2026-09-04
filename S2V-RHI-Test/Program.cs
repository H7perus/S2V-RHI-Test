//global using static S2vDevice;

using S2V_RHI_Test.RHI;
using S2V_RHI_Test.RHI.ShaderCompile;
using SDL;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using Vortice.Vulkan;
using static SDL.SDL3;
using static System.Net.Mime.MediaTypeNames;
using static Vortice.Vulkan.Vma;
using Buffer = S2V_RHI_Test.RHI.Buffer;

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

        //var spirv = slangCompiler.Compile(shaderString);

        var module = slangCompiler.LoadShaderModule(rootPath + "Shaders/testShaderDescriptorHandle.slang");

        SpecialisedShader specShader = slangCompiler.SpecialiseAndCompile(module, new Dictionary<string, int>() { { "isPurple", 1 } });

        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        Console.WriteLine(JsonSerializer.Serialize(specShader, options));

        var swapchain = new Swapchain(800, 600);

        bool running = true;

        var cmd = new CommandList(RenderDevice!.QueueFamilyIndices.GraphicsFamily!.Value);
        var transferCmd = new CommandList(RenderDevice!.QueueFamilyIndices.TransferFamily!.Value);

        var pipeline = new PipelineGraphics(specShader, VkFormat.B8G8R8A8Unorm);


        var uBuffer = new Buffer(1000, VkBufferUsageFlags.UniformBuffer, VmaMemoryUsage.GpuToCpu);

        var vAttribBuffer = new Buffer(1000, VkBufferUsageFlags.VertexBuffer, VmaMemoryUsage.GpuToCpu);

        var vPosBuffer = new Buffer(1000, VkBufferUsageFlags.VertexBuffer, VmaMemoryUsage.GpuToCpu);

        var iBuffer = new Buffer(1000, VkBufferUsageFlags.IndexBuffer, VmaMemoryUsage.GpuToCpu);

        Vector3* vBufferMap = (Vector3*)vPosBuffer.Map();

        Vector3[] positions =
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
        vPosBuffer.Unmap();

        uint* indexPtr = (uint*)iBuffer.Map();

        indexPtr[0] = 0;
        indexPtr[1] = 1;
        indexPtr[2] = 2;
        indexPtr[3] = 3;
        indexPtr[4] = 2;
        indexPtr[5] = 0;
        iBuffer.Unmap();

        float* bufferMap = (float*)vAttribBuffer.Map();

        float[] colors = new float[20]
            {
                1.0f, 0.0f, 0.5f, 1.0f, 0.0f,
                1.0f, 0.5f, 0.0f, 1.0f, 1.0f,
                1.0f, 0.5f, 0.0f, 0.0f, 1.0f,
                1.0f, 0.0f, 0.5f, 0.0f, 0.0f,
            };

        for (int i = 0; i < 20; i++)
        {
            bufferMap[i] = colors[i];
        }


        var imageAvailableSemaphore = RenderDevice!.CreateSemaphore();

        var fifFreedFence = RenderDevice.CreateFence(true);
        var stopwatch = Stopwatch.StartNew();

        byte[] GetRgbaPixels(string path)
        {
            using Bitmap bitmap = new Bitmap(path);
            using Bitmap rgba = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(rgba))
                g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);

            int width = rgba.Width;
            int height = rgba.Height;

            BitmapData data = rgba.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            byte[] pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                IntPtr rowPtr = data.Scan0 + y * data.Stride;
                Marshal.Copy(rowPtr, pixels, y * width * 4, width * 4);
            }

            rgba.UnlockBits(data);

            // swap B and R: GDI+ gives BGRA, Vulkan wants RGBA
            for (int i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);

            return pixels;
        }

        //Image image = Image.FromFile("../../../nomjpeg.jpg");

        var imgData = GetRgbaPixels("../../../nomjpeg.jpg");

        //var imgTestData = Enumerable.Repeat((byte)255, image.Width * image.Height * 4).ToArray();

        Texture testTexture = new Texture((uint)564, (uint)564);

        int padding = 48;

        // 1. Create a staging buffer sized exactly to your tightly-packed data
        VkBufferCreateInfo stagingInfo = new()
        {
            size = (ulong)(564 * 564 * 4) * 2,
            usage = VkBufferUsageFlags.TransferSrc,
            sharingMode = VkSharingMode.Exclusive
        };

        VmaAllocationCreateInfo stagingAlloc = new()
        {
            usage = VmaMemoryUsage.CpuOnly,
            flags = VmaAllocationCreateFlags.Mapped // maps it for you immediately
        };

        vmaCreateBuffer(RenderDevice!.VmaAllocator, &stagingInfo, &stagingAlloc, out var stagingBuffer, out var stagingAllocation, out var allocInfo);


        // 2. ONE memcpy — no row math needed, buffers are always tightly packed
        Marshal.Copy(imgData, 0, (nint)allocInfo.pMappedData, imgData.Length);

        
        var transferFence = RenderDevice.CreateFence();

        VkBufferImageCopy region = new()
        {
            bufferOffset = 0,
            bufferRowLength = 0,   // 0 = tightly packed, matches your staging buffer
            bufferImageHeight = 0,
            imageSubresource = new VkImageSubresourceLayers
            {
                aspectMask = VkImageAspectFlags.Color,
                mipLevel = 0,
                baseArrayLayer = 0,
                layerCount = 1
            },
            imageOffset = new VkOffset3D(0, 0, 0),
            imageExtent = new VkExtent3D(564, 564, 1)
        };

        transferCmd.Begin();

        RenderDevice!.VkDeviceApi.vkCmdCopyBufferToImage(transferCmd.Handle, stagingBuffer, testTexture.ImageHandle, VkImageLayout.TransferDstOptimal, 1, &region);

        transferCmd.End();

        RenderDevice!.SubmitTransfer(transferCmd, transferFence);

        RenderDevice.WaitForFences(transferFence);
        RenderDevice.ResetFences(transferFence);


        byte[] prevMipArray = imgData;

        for (int mipLevel = 1; mipLevel < 3; mipLevel++)
        {
            uint mipFactor = (uint)Math.Pow(2, mipLevel+1);

            uint mipDataSize = (uint)imgData.Length / mipFactor;

            byte[] mipData = new byte[mipDataSize];

            uint prevWidth = 564u >> (mipLevel-1);       // width of the source (previous) mip
            uint newWidth = 564u >> (mipLevel); // width of the mip being generated

            for (uint y = 0; y < newWidth; y++)
            {
                for (uint x = 0; x < newWidth; x++)
                {
                    uint srcX = x * 2;
                    uint srcY = y * 2;

                    uint TL = (srcX + srcY * prevWidth) * 4;
                    uint TR = (srcX + 1 + srcY * prevWidth) * 4;
                    uint BL = (srcX + (srcY + 1) * prevWidth) * 4;
                    uint BR = (srcX + 1 + (srcY + 1) * prevWidth) * 4;

                    uint dst = (x + y * newWidth) * 4;

                    for (uint c = 0; c < 4; c++)
                    {
                        float v1 = prevMipArray[TL + c] / 255f;
                        float v2 = prevMipArray[TR + c] / 255f;
                        float v3 = prevMipArray[BL + c] / 255f;
                        float v4 = prevMipArray[BR + c] / 255f;

                        mipData[dst + c] = (byte)((v1 + v2 + v3 + v4) / 4f * 255f);
                    }
                }
            }

            prevMipArray = mipData;

            region.imageSubresource.mipLevel = (uint)mipLevel;
            region.imageExtent = new VkExtent3D(564 / (uint)Math.Pow(2, mipLevel), 564 / (uint)Math.Pow(2, mipLevel), 1);

            Marshal.Copy(mipData, 0, (nint)allocInfo.pMappedData, mipData.Length);

            transferCmd.Begin();

            RenderDevice!.VkDeviceApi.vkCmdCopyBufferToImage(transferCmd.Handle, stagingBuffer, testTexture.ImageHandle, VkImageLayout.TransferDstOptimal, 1, &region);

            transferCmd.End();

            RenderDevice!.SubmitTransfer(transferCmd, transferFence);

            RenderDevice.WaitForFences(transferFence);
            RenderDevice.ResetFences(transferFence);
        }

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


            //RenderDevice!.VkDeviceApi.vkVie


            Matrix4x4 perspectiveMat = Matrix4x4.CreatePerspectiveFieldOfView(1.6f, 4f / 3f, 0.1f, 10f);

            Matrix4x4 viewMat = Matrix4x4.CreateFromYawPitchRoll(0.0f, 0f, (float)Math.PI / 4) * Matrix4x4.CreateFromYawPitchRoll((float)stopwatch.Elapsed.TotalSeconds, 0, 0) * Matrix4x4.CreateTranslation(new Vector3(0, 0, -1f));



            var samplerInfo = new VkSamplerCreateInfo
            {
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                mipmapMode = VkSamplerMipmapMode.Linear,
                mipLodBias = 0,
                anisotropyEnable = false,
                minLod = 1 + (float)Math.Sin((float)stopwatch.Elapsed.TotalSeconds * 5),
                maxLod = 1000,
            };



            var sampler = RenderDevice!.CreateSampler(samplerInfo);

            testTexture.SetSampler(sampler);

            *((Matrix4x4*)((float*)uBuffer.Map() + 16)) = viewMat * perspectiveMat; // perspectiveMat * viewMat; // perspectiveMat;

            uBuffer.Unmap();

            int imageIndex = swapchain.AcquireNextImage(imageAvailableSemaphore);
            cmd.Begin();


            uint[] descriptorHandleValue = { 0, 0 };

            cmd.PushConstants(uBuffer.DescriptorHandle);



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

            cmd.BindVertexBuffer(vPosBuffer, binding: 0);
            cmd.BindVertexBuffer(vAttribBuffer, binding: 1);

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