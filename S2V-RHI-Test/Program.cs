using S2V_RHI_Test.RHI.VK;

using System.Runtime.InteropServices;

using SDL;
using static SDL.SDL3;

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
            "My Vulkan App",
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

        return;
    }
}