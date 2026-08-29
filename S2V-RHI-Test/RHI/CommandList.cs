using Microsoft.VisualBasic.FileIO;
using System;
using Vortice.Vulkan;

namespace S2V_RHI_Test.RHI;

unsafe public class CommandList : IDisposable
{
    private readonly VkCommandPool _commandPool;

    public VkCommandBuffer Handle { get; }

    public CommandList()
    {
        var device = RenderDevice
            ?? throw new InvalidOperationException(
                "S2vDevice has not been initialized.");

        var poolInfo = new VkCommandPoolCreateInfo
        {
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
            queueFamilyIndex = device.QueueFamilyIndices.GraphicsFamily!.Value
        };

        var result = device.VkDeviceApi.vkCreateCommandPool(
            &poolInfo,
            null,
            out _commandPool);

        if (result != VkResult.Success)
            throw new Exception(
                $"Failed to create command pool: {result}");

        var allocInfo = new VkCommandBufferAllocateInfo
        {
            commandPool = _commandPool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = 1
        };

        VkCommandBuffer cmdBuff;

        result = device.VkDeviceApi.vkAllocateCommandBuffers(
            &allocInfo,
            &cmdBuff);

        if (result != VkResult.Success)
            throw new Exception(
                $"Failed to allocate command buffer: {result}");

        Handle = cmdBuff;
    }

    public void Begin()
    {
        var device = RenderDevice!;

        device.VkDeviceApi.vkResetCommandBuffer(
            Handle,
            0);

        var beginInfo = new VkCommandBufferBeginInfo
        {
            flags = VkCommandBufferUsageFlags.OneTimeSubmit
        };

        Check(
            device.VkDeviceApi.vkBeginCommandBuffer(
                Handle,
                &beginInfo),
            "vkBeginCommandBuffer");

        fixed (VkDescriptorSet* pSharedBindlessSet = &RenderDevice.SharedBindlessDescriptorSet)
        {
            RenderDevice!.VkDeviceApi.vkCmdBindDescriptorSets(
                Handle,
                pipelineBindPoint: VkPipelineBindPoint.Graphics,
                layout: RenderDevice.SharedPipelineLayout,
                firstSet: 1,
                descriptorSetCount: 1,
                pSharedBindlessSet,
                dynamicOffsetCount: 0,
                dynamicOffsets: null);
            RenderDevice!.VkDeviceApi.vkCmdBindDescriptorSets(
                Handle,
                pipelineBindPoint: VkPipelineBindPoint.Compute,
                layout: RenderDevice.SharedPipelineLayout,
                firstSet: 1,
                descriptorSetCount: 1,
                pSharedBindlessSet,
                dynamicOffsetCount: 0,
                dynamicOffsets: null);
        }
    }

    public void ClearSwapchainImage(
        VkImage image,
        ref VkImageLayout currentLayout,
        VkClearColorValue color)
    {
        var device = RenderDevice!;

        var preLayout = currentLayout;

        ColorImageTransition(
            image,
            ref currentLayout,
            VkImageLayout.TransferDstOptimal);


        var subresourceRange = new VkImageSubresourceRange
        {
            aspectMask = VkImageAspectFlags.Color,
            baseMipLevel = 0,
            levelCount = 1,
            baseArrayLayer = 0,
            layerCount = 1
        };

        device.VkDeviceApi.vkCmdClearColorImage(
            Handle,
            image,
            VkImageLayout.TransferDstOptimal,
            &color,
            1,
            &subresourceRange
            );

        ColorImageTransition(
            image,
            ref currentLayout,
            preLayout);
    }

    public void End()
    {
        var device = RenderDevice!;

        Check(
            device.VkDeviceApi.vkEndCommandBuffer(Handle),
            "vkEndCommandBuffer");
    }

    public void ColorImageTransition(
        VkImage image,
        ref VkImageLayout imageLayout,
        VkImageLayout targetLayout)
    {
        var device = RenderDevice!;


        var barrier = new VkImageMemoryBarrier2
        {
            oldLayout = imageLayout,
            newLayout = targetLayout,
            srcAccessMask = VkAccessFlags2.MemoryWrite,
            dstAccessMask = VkAccessFlags2.MemoryRead | VkAccessFlags2.MemoryWrite,
            image = image,
            subresourceRange = new VkImageSubresourceRange
            {
                aspectMask = VkImageAspectFlags.Color,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1
            }
        };

        PipelineBarrier(new[] { barrier });
        imageLayout = targetLayout;
    }

    public void PipelineBarrier(ReadOnlySpan<VkImageMemoryBarrier2> imageMemoryBarriers = new ReadOnlySpan<VkImageMemoryBarrier2>(), ReadOnlySpan<VkBufferMemoryBarrier2> bufferMemoryBarriers = new ReadOnlySpan<VkBufferMemoryBarrier2>(), ReadOnlySpan<VkMemoryBarrier2> memoryBarriers = new ReadOnlySpan<VkMemoryBarrier2>())
    {
        fixed (VkImageMemoryBarrier2* pImageMemoryBarriers = imageMemoryBarriers)
        fixed (VkBufferMemoryBarrier2* pBufferMemoryBarriers = bufferMemoryBarriers)
        fixed (VkMemoryBarrier2* pMemoryBarriers =  memoryBarriers)
        {
            VkDependencyInfo depInfo = new VkDependencyInfo
            {
                memoryBarrierCount = (uint)memoryBarriers.Length,
                pMemoryBarriers = pMemoryBarriers,
                bufferMemoryBarrierCount = (uint)bufferMemoryBarriers.Length,
                pBufferMemoryBarriers = pBufferMemoryBarriers,
                imageMemoryBarrierCount = (uint)imageMemoryBarriers.Length,
                pImageMemoryBarriers = pImageMemoryBarriers
            };

            RenderDevice!.VkDeviceApi.vkCmdPipelineBarrier2(Handle, &depInfo);
        }
        //H7per: TODO: We should warn if nothing was submitted.
    }

    public void BindGraphicsPipeline(PipelineGraphics pipeline)
    {
        RenderDevice!.VkDeviceApi.vkCmdBindPipeline(Handle, VkPipelineBindPoint.Graphics, pipeline.HandlePipeline);
    }

    public void BindVertexBuffer(Buffer vertexBuffer, uint binding = 0)
    {
        RenderDevice!.VkDeviceApi.vkCmdBindVertexBuffer(Handle, binding, vertexBuffer.Handle);
    }

    public void BindIndexBuffer(Buffer indexBuffer)
    {
        RenderDevice!.VkDeviceApi.vkCmdBindIndexBuffer(Handle, indexBuffer.Handle, 0, VkIndexType.Uint32);
    }

    //Might need an overload to set multiple.
    public void SetViewport(VkViewport viewport)
    {
        RenderDevice!.VkDeviceApi.vkCmdSetViewport(Handle, 0, viewport);
    }

    public void SetScissor(VkRect2D scissor)
    {
        RenderDevice!.VkDeviceApi.vkCmdSetScissor(Handle, 0, scissor);
    }

    public void PushConstants<T>(T data, uint offset = 0) where T : struct
    {
        if (offset + sizeof(T) > 8)
        {
            throw new ArgumentException($"The size of {nameof(T)} exceeds the available push constant range");
        }
        RenderDevice!.VkDeviceApi.vkCmdPushConstants(Handle, RenderDevice!.SharedPipelineLayout, VkShaderStageFlags.All, offset, (uint)sizeof(T), &data);
    }

    public void BeginRendering(VkRenderingInfo renderingInfo)
    {
        RenderDevice!.VkDeviceApi.vkCmdBeginRendering(Handle, &renderingInfo);
    }

    public void EndRendering()
    {
        RenderDevice!.VkDeviceApi.vkCmdEndRendering(Handle);
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        RenderDevice!.VkDeviceApi.vkCmdDraw(Handle, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        RenderDevice!.VkDeviceApi.vkCmdDrawIndexed(Handle, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    private static void Check(
        VkResult result,
        string operation)
    {
        if (result != VkResult.Success)
            throw new Exception(
                $"{operation} failed: {result}");
    }

    public void Dispose()
    {
        var device = RenderDevice;

        if (device == null)
            return;

        device.VkDeviceApi.vkDestroyCommandPool(
            _commandPool,
            null);
    }
}