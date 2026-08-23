using Microsoft.VisualBasic.FileIO;
using System;
using Vortice.Vulkan;

namespace S2V_RHI_Test.RHI.VK;

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
    }

    public void ClearSwapchainImage(
        VkImage image,
        VkClearColorValue color)
    {
        var device = RenderDevice!;

        TransitionImage(
            image,
            VkImageLayout.ColorAttachmentOptimal,
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

        TransitionImage(
            image,
            VkImageLayout.TransferDstOptimal,
            VkImageLayout.ColorAttachmentOptimal);
    }

    public void End()
    {
        var device = RenderDevice!;

        Check(
            device.VkDeviceApi.vkEndCommandBuffer(Handle),
            "vkEndCommandBuffer");
    }

    public void TransitionImage(
        VkImage image,
        VkImageLayout oldLayout,
        VkImageLayout newLayout)
    {
        var device = RenderDevice!;

        VkPipelineStageFlags srcStage;
        VkPipelineStageFlags dstStage;
        VkAccessFlags srcAccess;
        VkAccessFlags dstAccess;

        if (oldLayout == VkImageLayout.ColorAttachmentOptimal &&
            newLayout == VkImageLayout.TransferDstOptimal)
        {
            srcStage = VkPipelineStageFlags.BottomOfPipe;
            dstStage = VkPipelineStageFlags.Transfer;

            srcAccess = 0;
            dstAccess = VkAccessFlags.TransferWrite;
        }
        else if (oldLayout == VkImageLayout.TransferDstOptimal &&
                 newLayout == VkImageLayout.ColorAttachmentOptimal)
        {
            srcStage = VkPipelineStageFlags.Transfer;
            dstStage = VkPipelineStageFlags.BottomOfPipe;

            srcAccess = VkAccessFlags.TransferWrite;
            dstAccess = 0;
        }
        else
        {
            throw new ArgumentException(
                $"Unsupported image transition: " +
                $"{oldLayout} -> {newLayout}");
        }

        var barrier = new VkImageMemoryBarrier
        {
            oldLayout = oldLayout,
            newLayout = newLayout,
            srcAccessMask = srcAccess,
            dstAccessMask = dstAccess,
            srcQueueFamilyIndex = uint.MaxValue,
            dstQueueFamilyIndex = uint.MaxValue,
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

        device.VkDeviceApi.vkCmdPipelineBarrier(
            Handle,
            srcStage,
            dstStage,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier);
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

    public void BindGraphicsPipeline(Pipeline pipeline)
    {
        RenderDevice!.VkDeviceApi.vkCmdBindPipeline(Handle, VkPipelineBindPoint.Graphics, pipeline.VkPipeline);
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