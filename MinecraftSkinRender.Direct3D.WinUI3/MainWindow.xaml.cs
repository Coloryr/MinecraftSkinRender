using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using SkiaSharp;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MinecraftSkinRender.Direct3D.WinUI3;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
    public interface ISwapChainPanelNative
    {
        [PreserveSig] HResult SetSwapChain([In] IntPtr swapChain);
        [PreserveSig] ulong Release();
    }

    private SkinRenderDX11 skin;

    private static bool havecape = true;
    private ComPtr<IDXGISwapChain1> swap;

    public MainWindow()
    {
        InitializeComponent();

        DXPanel.Loaded += DXPanel_Loaded;
        DXPanel.SizeChanged += DXPanel_SizeChanged;

        CompositionTarget.Rendering += (sender, obj) => {
            skin.Rot(0, 1f);
            skin?.DX11Render();
        };
    }

    private void DXPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (skin != null)
        {
            skin.Width = (int)e.NewSize.Width;
            skin.Height = (int)e.NewSize.Height;
        }
    }

    private unsafe async void DXPanel_Loaded(object sender, RoutedEventArgs e)
    {
        var d3d11 = D3D11.GetApi();
        ComPtr<ID3D11Device> device = default;
        ComPtr<ID3D11DeviceContext> context = default;
        d3d11.CreateDevice(default(ComPtr<IDXGIAdapter>), D3DDriverType.Hardware, 0, (uint)CreateDeviceFlag.BgraSupport, null, 0, D3D11.SdkVersion, ref device, null, ref context);

        var dxgi = DXGI.GetApi();
        using var factory = dxgi.CreateDXGIFactory<IDXGIFactory2>();

        var desc = new SwapChainDesc1
        {
            Width = 1,
            Height = 1,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            BufferUsage = DXGI.UsageRenderTargetOutput,
            BufferCount = 2,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential,
            AlphaMode = AlphaMode.Premultiplied
        };

        ComPtr<IDXGIOutput> nullOutput = default;

        HResult res = factory.CreateSwapChainForComposition(
            device,
            in desc,
            nullOutput,
            ref swap
        );

        if (res.IsFailure) throw new Exception($"Failed to create SwapChain: {res}");

        SilkMarshal.ThrowHResult(res);

        skin = new SkinRenderDX11(device, context, swap);

        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "skin.png");
        var img = SKBitmap.Decode(path);
        skin.SetSkinTex(img);
        skin.SkinType = SkinType.NewSlim;
        skin.EnableTop = true;
        skin.RenderType = SkinRenderType.Normal;
        skin.Animation = true;
        skin.EnableCape = true;
        if (havecape)
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "cape.png");
            skin.SetCapeTex(SKBitmap.Decode(path));
        }
        skin.FpsUpdate += (a, b) =>
        {
            Console.WriteLine("Fps: " + b);
        };
        skin.BackColor = new(1, 1, 1, 1);
        skin.Width = (int)DXPanel.ActualWidth;
        skin.Height = (int)DXPanel.ActualHeight;

        skin.DX11Init();

        var panelNative = WinRT.CastExtensions.As<ISwapChainPanelNative>(DXPanel);
        int hr = panelNative.SetSwapChain((nint)swap.Handle);

        if (hr < 0)
        {
            throw new Exception($"SetSwapChain 失败，HRESULT: 0x{hr:X}");
        }


    }
}
