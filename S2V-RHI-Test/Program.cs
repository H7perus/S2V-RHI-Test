using S2V_RHI_Test.RHI.VK;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;
using SilkVk = Silk.NET.Vulkan;


namespace HelloTriangle;

public static class Program
{
    public static void Main()
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(800, 600);
        using var window = Window.Create(options);
        window.Initialize(); // must call before window.VkSurface is populated


        //From here on, we can use the device!
        createS2vDevice(window);

        return;
    }
}