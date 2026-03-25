using System.Numerics;
using System.Runtime.InteropServices;

namespace MinecraftSkinRender.Direct3D;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct VertexDX11
{
    public Vector3 Position;
    public Vector2 UV;
    public Vector3 Normal;
}
