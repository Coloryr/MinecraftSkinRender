using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace MinecraftSkinRender.Direct3D;

internal record DXItem : IDisposable
{
    public ComPtr<ID3D11Buffer> VertexBuffer;
    public ComPtr<ID3D11Buffer> IndexBuffer;
    public uint IndexCount;

    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
    }
}
