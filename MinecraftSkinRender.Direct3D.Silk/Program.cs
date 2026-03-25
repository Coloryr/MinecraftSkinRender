using Silk.NET.Maths;
using Silk.NET.Windowing;
using SkiaSharp;

namespace MinecraftSkinRender.Direct3D.Silk;

internal class Program
{
    private static IWindow window;

    private static SkinRenderDX11 skin;

    private static bool havecape = true;

    static async Task Main(string[] args)
    {
        await SkinDownloader.Download();

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Direct3D11";
        options.API = GraphicsAPI.None; // <-- This bit is important, as your window will be configured for OpenGL by default.
        window = Window.Create(options);

        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.FramebufferResize += OnFramebufferResize;

        // Run the window.
        window.Run();

        //dispose the window, and its internal resources
        window.Dispose();

    }

    static unsafe void OnLoad()
    {
        skin = new SkinRenderDX11(window);

        var img = SKBitmap.Decode("skin.png");
        skin.SetSkinTex(img);
        skin.SkinType = SkinType.NewSlim;
        skin.EnableTop = true;
        skin.RenderType = SkinRenderType.Normal;
        skin.Animation = true;
        skin.EnableCape = true;
        if (havecape)
        {
            skin.SetCapeTex(SKBitmap.Decode("cape.png"));
        }
        skin.FpsUpdate += (a, b) =>
        {
            Console.WriteLine("Fps: " + b);
        };
        skin.BackColor = new(1, 1, 1, 1);
        skin.Width = window.FramebufferSize.X;
        skin.Height = window.FramebufferSize.Y;

        skin.DX11Init();
    }

    static void OnUpdate(double deltaSeconds)
    {
        if (skin == null)
        {
            return;
        }
        skin.Rot(0, 1f);
        skin.Tick(deltaSeconds);
    }

    static unsafe void OnFramebufferResize(Vector2D<int> newSize)
    {
        if (skin == null)
        {
            return;
        }
        skin.Width = newSize.X;
        skin.Height = newSize.Y;
    }

    static unsafe void OnRender(double deltaSeconds)
    {
        if (skin == null)
        {
            return;
        }
        skin.DX11Render();
    }
}
