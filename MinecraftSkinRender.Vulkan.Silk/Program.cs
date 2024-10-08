﻿//using System.Text;
//using MinecraftSkinRender.MojangApi;
//using Newtonsoft.Json;
using MinecraftSkinRender.Vulkan.KHR;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using SkiaSharp;

namespace MinecraftSkinRender.Vulkan.Silk;

internal class Program : IVulkanApi
{
    static void Main(string[] args)
    {
        new Program().Start();
    }

    private IWindow _window;

    public async void Start()
    {
        bool havecape = true;

        Console.WriteLine("Download skin");

        //var res = await MinecraftAPI.GetMinecraftProfileNameAsync("Color_yr");
        //var res1 = await MinecraftAPI.GetUserProfile(res!.UUID);
        //TexturesObj? obj = null;
        //foreach (var item in res1!.properties)
        //{
        //    if (item.name == "textures")
        //    {
        //        var temp = Convert.FromBase64String(item.value);
        //        var data = Encoding.UTF8.GetString(temp);
        //        obj = JsonConvert.DeserializeObject<TexturesObj>(data);
        //        break;
        //    }
        //}
        //if (obj == null)
        //{
        //    Console.WriteLine("No have skin");
        //    return;
        //}
        //if (obj!.textures.SKIN.url != null)
        //{
        //    var data = await MinecraftAPI.Client.GetByteArrayAsync(obj!.textures.SKIN.url);
        //    File.WriteAllBytes("skin.png", data);
        //}
        //if (obj.textures.CAPE.url != null)
        //{
        //    var data = await MinecraftAPI.Client.GetByteArrayAsync(obj!.textures.CAPE.url);
        //    File.WriteAllBytes("cape.png", data);
        //    havecape = true;
        //}

        //Create a window.
        var options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(400, 400),
            Title = "Vulkan",
            FramesPerSecond = 60
        };

        _window = Window.Create(options)!;
        _window.Initialize();

        if (_window.VkSurface is null)
        {
            throw new Exception("Windowing platform doesn't support Vulkan.");
        }

        var skin = new SkinRenderVulkanKHR(Vk.GetApi(), this);

        _window.Resize += (size) =>
        {
            skin.Width = size.X;
            skin.Height = size.Y;
        };

        _window.Render += (time) =>
        {
            skin.Rot(0, 1f);
            skin.Tick(time);
            skin.VulkanRender();
        };

        skin.Width = _window.FramebufferSize.X;
        skin.Height = _window.FramebufferSize.Y;
        skin.SetBackColor(new(0, 1, 0, 1));
        var img = SKBitmap.Decode("skin.png");
        skin.SetSkin(img);
        skin.SetSkinType(SkinType.NewSlim);
        skin.SetCape(SKBitmap.Decode("cape.png"));
        skin.SetTopModel(true);
        skin.SetCape(true);
        skin.SetMSAA(false);
        skin.SetAnimation(true);
        skin.FpsUpdate += (a, b) =>
        {
            Console.WriteLine("Fps: " + b);
        };
        skin.VulkanInit();

        _ = Task.Run(() =>
        {
            while (true)
            {
                Thread.Sleep(2000);
                //skin.SetSkin(img);
            }
        });

        _window.Run();
    }

    public unsafe IReadOnlyList<string> GetRequiredExtensions()
    {
        var glfwExtensions = _window.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);

        return extensions;
    }

    public unsafe SurfaceKHR CreateSurface(Instance instance)
    {
        return _window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();
    }
}
