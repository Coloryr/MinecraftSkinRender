﻿using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;

namespace MinecraftSkinRender.OpenGL.Silk;

internal class Program
{
    static async Task Main(string[] args)
    {
        bool havecape = true;
        //Console.WriteLine("Download skin");

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

        // Create a Silk.NET window as usual
        using var window = Window.Create(WindowOptions.Default with
        {
            API = GraphicsAPI.Default with
            {
                API = ContextAPI.OpenGLES,
                Version = new(3, 2)
            },
            Size = new(400, 400),
            VSync = true
        });

        // Declare some variables
        GL gl = null;
        SkinRenderOpenGL? skin = null;

        // Our loading function
        window.Load += () =>
        {
            gl = window.CreateOpenGL();
            skin = new(new SlikOpenglApi(gl))
            {
                IsGLES = true
            };
            skin.SetSkin(SKBitmap.Decode("skin.png"));
            skin.SetSkinType(SkinType.NewSlim);
            skin.SetTopModel(true);
            skin.SetMSAA(false);
            skin.SetAnimation(true);
            skin.SetCape(true);
            if (havecape)
            {
                skin.SetCape(SKBitmap.Decode("cape.png"));
            }
            skin.FpsUpdate += (a, b) =>
            {
                Console.WriteLine("Fps: " + b);
            };
            skin.SetBackColor(new(0, 1, 0, 1));
            skin.Width = window.FramebufferSize.X;
            skin.Height = window.FramebufferSize.Y;
            skin.OpenGlInit();
        };

        // Handle resizes
        window.FramebufferResize += s =>
        {
            if (skin == null)
            {
                return;
            }
            skin.Width = s.X;
            skin.Height = s.Y;
        };

        // The render function
        window.Render += delta =>
        {
            if (skin == null)
            {
                return;
            }
            skin.Rot(0, 1f);
            skin.Tick(delta);
            skin.OpenGlRender(0);
            //gl.Clear(ClearBufferMask.ColorBufferBit);
            //gl.ClearColor(0, 0, 1, 0);
            //window.SwapBuffers();
        };

        // The closing function
        window.Closing += () =>
        {
            if (skin == null)
            {
                return;
            }
            skin.OpenGlDeinit();
            // Unload OpenGL
            gl?.Dispose();
        };

        // Now that everything's defined, let's run this bad boy!
        window.Run();

        window.Dispose();
    }
}
