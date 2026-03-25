using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace MinecraftSkinRender.Direct3D;

public partial class SkinRenderDX11 : SkinRender
{
    private bool _init = false;
    private uint _width, _height;

    private ComPtr<ID3D11Device> _device;
    private ComPtr<ID3D11DeviceContext> _deviceContext;
    private ComPtr<IDXGIFactory2> _factory;
    private ComPtr<IDXGISwapChain1> _swapChain;
    private ComPtr<ID3D11RasterizerState> _rasterState;
    private ComPtr<ID3D11DepthStencilState> _depthStateEnable;
    private ComPtr<ID3D11DepthStencilState> _depthStateWriteDisable;
    private ComPtr<ID3D11BlendState> _blendStateAlpha;
    private ComPtr<ID3D11BlendState> _blendStateDisable;
    private ComPtr<ID3D11RenderTargetView> _renderTargetView;
    private ComPtr<ID3D11DepthStencilView> _depthStencilView;

    public unsafe SkinRenderDX11(INativeWindowSource window, bool forceDxvk = false)
    {
        var dxgi = DXGI.GetApi(window, forceDxvk);
        var d3d11 = D3D11.GetApi(window, forceDxvk);

        var res = d3d11.CreateDevice(
            default(ComPtr<IDXGIAdapter>),
            D3DDriverType.Hardware,
            Software: default,
            (uint)CreateDeviceFlag.Debug,
            null,
            0,
            D3D11.SdkVersion,
            ref _device,
            null,
            ref _deviceContext);

        SilkMarshal.ThrowHResult(res);

        if (OperatingSystem.IsWindows())
        {
            _device.SetInfoQueueCallback(msg => Console.WriteLine(SilkMarshal.PtrToString((nint)msg.PDescription)));
        }

        var swapChainDesc = new SwapChainDesc1
        {
            BufferCount = 2, // double buffered
            Format = Format.FormatB8G8R8A8Unorm,
            BufferUsage = DXGI.UsageRenderTargetOutput,
            SwapEffect = SwapEffect.FlipDiscard,
            SampleDesc = new SampleDesc(1, 0)
        };

        _factory = dxgi.CreateDXGIFactory<IDXGIFactory2>();

        res = _factory.CreateSwapChainForHwnd(
                _device,
                window.Native!.DXHandle!.Value,
                in swapChainDesc,
                null,
                ref Unsafe.NullRef<IDXGIOutput>(),
                ref _swapChain);

        SilkMarshal.ThrowHResult(res);
    }

    public SkinRenderDX11(ComPtr<ID3D11Device> device, ComPtr<ID3D11DeviceContext> context, ComPtr<IDXGISwapChain1> swap)
    {
        _device = device;
        _deviceContext = context;

        _swapChain = swap;
    }

    public void DX11Init()
    {
        if (_init) return;
        _init = true;

        InitShader();
        InitTexture();
        InitStates();
    }

    private void InitStates()
    {
        var rasterDesc = new RasterizerDesc
        {
            CullMode = CullMode.Back,
            FillMode = FillMode.Solid,
            FrontCounterClockwise = true,
            DepthClipEnable = true
        };
        _device.CreateRasterizerState(in rasterDesc, ref _rasterState);

        var dsDesc = new DepthStencilDesc
        {
            DepthEnable = true,
            DepthWriteMask = DepthWriteMask.All,
            DepthFunc = ComparisonFunc.LessEqual
        };
        _device.CreateDepthStencilState(in dsDesc, ref _depthStateEnable);

        dsDesc.DepthWriteMask = DepthWriteMask.Zero;
        _device.CreateDepthStencilState(in dsDesc, ref _depthStateWriteDisable);

        var blendDesc = new BlendDesc();
        blendDesc.RenderTarget[0] = new RenderTargetBlendDesc
        {
            BlendEnable = true,
            SrcBlend = Blend.SrcAlpha,
            DestBlend = Blend.InvSrcAlpha,
            BlendOp = BlendOp.Add,
            SrcBlendAlpha = Blend.One,
            DestBlendAlpha = Blend.Zero,
            BlendOpAlpha = BlendOp.Add,
            RenderTargetWriteMask = (byte)ColorWriteEnable.All
        };
        _device.CreateBlendState(in blendDesc, ref _blendStateAlpha);

        blendDesc.RenderTarget[0].BlendEnable = false;
        _device.CreateBlendState(in blendDesc, ref _blendStateDisable);
    }

    private unsafe void InitFrameBuffer(uint width, uint height)
    {
        _renderTargetView.Dispose();
        _depthStencilView.Dispose();

        var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _device.CreateRenderTargetView(backBuffer, null, ref _renderTargetView);
        backBuffer.Dispose();

        var dsDesc = new Texture2DDesc
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatD24UnormS8Uint,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.DepthStencil
        };

        ComPtr<ID3D11Texture2D> depthBuffer = default;
        _device.CreateTexture2D(in dsDesc, null, ref depthBuffer);
        _device.CreateDepthStencilView(depthBuffer, null, ref _depthStencilView);
        depthBuffer.Dispose();

        _width = width;
        _height = height;
    }

    private unsafe void UpdateConstantBuffer(Matrix4x4 selfMat)
    {
        var cb = new ModelConstantBuffer
        {
            Model = Matrix4x4.Transpose(GetMatrix4(ModelPartType.Model)),
            Projection = Matrix4x4.Transpose(GetMatrix4(ModelPartType.Proj)),
            View = Matrix4x4.Transpose(GetMatrix4(ModelPartType.View)),
            Self = Matrix4x4.Transpose(selfMat)
        };

        MappedSubresource mappedResource = default;

        HResult hr = _deviceContext.Map(constantBuffer, 0, Map.WriteDiscard, 0, ref mappedResource);

        if (hr.IsSuccess)
        {
            // 将结构体拷贝到映射的内存中
            Unsafe.Copy(mappedResource.PData, ref cb);

            _deviceContext.Unmap(constantBuffer, 0);
        }
    }

    private unsafe void DrawItem(DXItem item, Matrix4x4 selfMat, ComPtr<ID3D11ShaderResourceView> tex)
    {
        if (item.VertexBuffer.Handle == null) return;

        UpdateConstantBuffer(selfMat);

        // 绑定纹理和采样器
        _deviceContext.PSSetShaderResources(0, 1, ref tex);
        _deviceContext.PSSetSamplers(0, 1, ref _samplerNearest);

        // 绑定顶点和索引缓冲
        uint stride = (uint)Unsafe.SizeOf<VertexDX11>();
        uint offset = 0;
        _deviceContext.IASetVertexBuffers(0, 1, ref item.VertexBuffer, in stride, in offset);
        _deviceContext.IASetIndexBuffer(item.IndexBuffer, Format.FormatR16Uint, 0);

        // 绘制
        _deviceContext.DrawIndexed(item.IndexCount, 0, 0);
    }

    private void DrawSkin(ModelDX model)
    {
        // 依次绘制各个部位
        DrawItem(model.Body, Matrix4x4.Identity, _textureSkin);
        DrawItem(model.Head, GetMatrix4(ModelPartType.Head), _textureSkin);
        DrawItem(model.LeftArm, GetMatrix4(ModelPartType.LeftArm), _textureSkin);
        DrawItem(model.RightArm, GetMatrix4(ModelPartType.RightArm), _textureSkin);
        DrawItem(model.LeftLeg, GetMatrix4(ModelPartType.LeftLeg), _textureSkin);
        DrawItem(model.RightLeg, GetMatrix4(ModelPartType.RightLeg), _textureSkin);
    }

    private void DrawCape()
    {
        if (HaveCape && _enableCape)
        {
            DrawItem(_normalModel.Cape, GetMatrix4(ModelPartType.Cape), _textureCape);
        }
    }

    public unsafe void DX11Render()
    {
        if (_switchSkin)
        {
            LoadSkin();
        }
        if (_switchModel)
        {
            LoadModel();
        }

        if (!HaveSkin)
        {
            return;
        }

        if (Width == 0 || Height == 0)
        {
            return;
        }

        if (Width != _width || Height != _height)
        {
            // DX11 缩放必须先解除绑定并释放引用
            _deviceContext.OMSetRenderTargets(0, (ID3D11RenderTargetView**)null, (ID3D11DepthStencilView*)null);

            // 缩放交换链
            _swapChain.ResizeBuffers(0, (uint)Width, (uint)Height, Format.FormatUnknown, 0);
            InitFrameBuffer((uint)Width, (uint)Height);
        }

        var rtv = _renderTargetView;
        var dsv = _depthStencilView;
        _deviceContext.OMSetRenderTargets(1, ref rtv, dsv);

        // 设置视口
        var viewport = new Viewport(0, 0, Width, Height, 0, 1);
        _deviceContext.RSSetViewports(1, in viewport);

        // 清屏
        float[] bgColor = { _backColor.X, _backColor.Y, _backColor.Z, _backColor.W };
        _deviceContext.ClearRenderTargetView(rtv, ref bgColor[0]);
        _deviceContext.ClearDepthStencilView(dsv, (uint)ClearFlag.Depth, 1.0f, 0);

        // 设置基本管线状态
        _deviceContext.IASetPrimitiveTopology(D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist);
        _deviceContext.IASetInputLayout(inputLayout);
        _deviceContext.VSSetShader(vertexShader, null, 0);
        _deviceContext.PSSetShader(pixelShader, null, 0);
        _deviceContext.VSSetConstantBuffers(0, 1, ref constantBuffer);
        _deviceContext.RSSetState(_rasterState);

        _deviceContext.OMSetDepthStencilState(_depthStateEnable, 0);
        _deviceContext.OMSetBlendState(_blendStateDisable, null, 0xFFFFFFFF);

        DrawSkin(_normalModel);
        DrawCape();

        if (_enableTop)
        {
            _deviceContext.OMSetDepthStencilState(_depthStateWriteDisable, 0);
            _deviceContext.OMSetBlendState(_blendStateAlpha, null, 0xFFFFFFFF);

            DrawSkin(_topModel);

            _deviceContext.OMSetDepthStencilState(_depthStateEnable, 0);
        }

        _swapChain.Present(1, 0);
    }

    public void DX11Deinit()
    {
        _factory.Dispose();
        _swapChain.Dispose();
        _device.Dispose();
        _deviceContext.Dispose();

        DeleteModel();
        DeleteTexture();
        DeleteShader();

        _rasterState.Dispose();
        _depthStateEnable.Dispose();
        _depthStateWriteDisable.Dispose();
        _blendStateAlpha.Dispose();
        _blendStateDisable.Dispose();
    }
}
