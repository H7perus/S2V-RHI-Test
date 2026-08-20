global using static Renderer.VkApi;
using Silk.NET.Vulkan;


namespace Renderer;

public static class VkApi
{
    public static readonly Vk vk = Vk.GetApi();
}