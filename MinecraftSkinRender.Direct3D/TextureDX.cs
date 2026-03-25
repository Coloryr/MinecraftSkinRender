using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using SkiaSharp;

namespace MinecraftSkinRender.Direct3D;

public partial class SkinRenderDX11
{
    private ComPtr<ID3D11ShaderResourceView> _textureSkin;
    private ComPtr<ID3D11ShaderResourceView> _textureCape;
    private ComPtr<ID3D11SamplerState> _samplerNearest;

    private unsafe void InitTexture()
    {
        var samplerDesc = new SamplerDesc
        {
            Filter = Filter.MinMagMipPoint,
            AddressU = TextureAddressMode.Border,
            AddressV = TextureAddressMode.Border,
            AddressW = TextureAddressMode.Border,
            ComparisonFunc = ComparisonFunc.Never,
            MinLOD = 0,
            MaxLOD = float.MaxValue,
        };

        samplerDesc.BorderColor[0] = 0;
        samplerDesc.BorderColor[1] = 0;
        samplerDesc.BorderColor[2] = 0;
        samplerDesc.BorderColor[3] = 0;

        _device.CreateSamplerState(in samplerDesc, ref _samplerNearest);
    }

    private unsafe void LoadTex(SKBitmap image, ref ComPtr<ID3D11ShaderResourceView> srv)
    {
        if (srv.Handle != null)
        {
            srv.Dispose();
        }

        var dxgiFormat = image.ColorType switch
        {
            SKColorType.Bgra8888 => Format.FormatB8G8R8A8Unorm,
            SKColorType.Rgba8888 => Format.FormatR8G8B8A8Unorm,
            _ => Format.FormatR8G8B8A8Unorm
        };

        var desc = new Texture2DDesc
        {
            Width = (uint)image.Width,
            Height = (uint)image.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = dxgiFormat,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Immutable,
            BindFlags = (uint)BindFlag.ShaderResource,
            CPUAccessFlags = 0,
            MiscFlags = 0
        };

        var initData = new SubresourceData
        {
            PSysMem = (void*)image.GetPixels(),
            SysMemPitch = (uint)(image.Width * image.BytesPerPixel),
            SysMemSlicePitch = (uint)(image.Width * image.Height * image.BytesPerPixel)
        };

        ComPtr<ID3D11Texture2D> texture = default;
        _device.CreateTexture2D(in desc, in initData, ref texture);
        _device.CreateShaderResourceView(texture, null, ref srv);

        texture.Dispose();
    }

    private void LoadSkin()
    {
        if (_skinTex == null)
        {
            OnErrorChange(ErrorType.SkinNotFound);
            return;
        }

        if (_skinType == SkinType.Unkonw)
        {
            OnErrorChange(ErrorType.UnknowSkinType);
            return;
        }

        LoadTex(_skinTex, ref _textureSkin);

        if (_cape != null)
        {
            LoadTex(_cape, ref _textureCape);
        }

        _switchSkin = false;
        _switchModel = true;
    }

    private unsafe void DeleteTexture()
    {
        if (_textureSkin.Handle != null) _textureSkin.Dispose();
        if (_textureCape.Handle != null) _textureCape.Dispose();
        if (_samplerNearest.Handle != null) _samplerNearest.Dispose();
    }
}
