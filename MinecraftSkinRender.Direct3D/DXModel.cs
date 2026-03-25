using System.Runtime.InteropServices;
using Silk.NET.Direct3D11;

namespace MinecraftSkinRender.Direct3D;

public partial class SkinRenderDX11
{
    private readonly ModelDX _normalModel = new();
    private readonly ModelDX _topModel = new();

    private uint _steveModelDrawOrderCount;

    private void DeleteModel()
    {
        _normalModel.Dispose();
        _topModel.Dispose();
    }

    private unsafe void CreateDXItem(ref DXItem item, CubeModelItemObj model, float[] uv)
    {
        item.Dispose();

        int size = model.Model.Length / 3;
        var points = new VertexDX11[size];

        for (var i = 0; i < size; i++)
        {
            var srci = i * 3;
            var srci1 = i * 2;
            points[i] = new VertexDX11
            {
                Position = new(model.Model[srci], model.Model[srci + 1], model.Model[srci + 2]),
                UV = new(uv[srci1], uv[srci1 + 1]),
                Normal = new(CubeModel.Vertices[srci], CubeModel.Vertices[srci + 1], CubeModel.Vertices[srci + 2])
            };
        }

        BufferDesc vDesc = new BufferDesc
        {
            ByteWidth = (uint)(points.Length * Marshal.SizeOf<VertexDX11>()),
            Usage = Usage.Immutable,
            BindFlags = (uint)BindFlag.VertexBuffer,
            CPUAccessFlags = 0,
            MiscFlags = 0,
            StructureByteStride = 0
        };

        fixed (void* pData = points)
        {
            SubresourceData initData = new SubresourceData { PSysMem = pData };
            _device.CreateBuffer(in vDesc, in initData, ref item.VertexBuffer);
        }

        item.IndexCount = (uint)model.Point.Length;
        BufferDesc iDesc = new BufferDesc
        {
            ByteWidth = (uint)(model.Point.Length * sizeof(ushort)),
            Usage = Usage.Immutable,
            BindFlags = (uint)BindFlag.IndexBuffer,
            CPUAccessFlags = 0,
            MiscFlags = 0,
            StructureByteStride = 0
        };

        fixed (void* pData = model.Point)
        {
            SubresourceData initData = new SubresourceData { PSysMem = pData };
            _device.CreateBuffer(in iDesc, in initData, ref item.IndexBuffer);
        }
    }

    private void LoadModel()
    {
        var normal = Steve3DModel.GetSteve(_skinType);
        var top = Steve3DModel.GetSteveTop(_skinType);
        var tex = Steve3DTexture.GetSteveTexture(_skinType);
        var textop = Steve3DTexture.GetSteveTextureTop(_skinType);

        _steveModelDrawOrderCount = (uint)normal.Head.Point.Length;

        // 加载基础层
        CreateDXItem(ref _normalModel.Head, normal.Head, tex.Head);
        CreateDXItem(ref _normalModel.Body, normal.Body, tex.Body);
        CreateDXItem(ref _normalModel.LeftArm, normal.LeftArm, tex.LeftArm);
        CreateDXItem(ref _normalModel.RightArm, normal.RightArm, tex.RightArm);
        CreateDXItem(ref _normalModel.LeftLeg, normal.LeftLeg, tex.LeftLeg);
        CreateDXItem(ref _normalModel.RightLeg, normal.RightLeg, tex.RightLeg);
        CreateDXItem(ref _normalModel.Cape, normal.Cape, tex.Cape);

        // 加载第二层 (Top Layer)
        if (_skinType != SkinType.Old)
        {
            CreateDXItem(ref _topModel.Head, top.Head, textop.Head);
            CreateDXItem(ref _topModel.Body, top.Body, textop.Body);
            CreateDXItem(ref _topModel.LeftArm, top.LeftArm, textop.LeftArm);
            CreateDXItem(ref _topModel.RightArm, top.RightArm, textop.RightArm);
            CreateDXItem(ref _topModel.LeftLeg, top.LeftLeg, textop.LeftLeg);
            CreateDXItem(ref _topModel.RightLeg, top.RightLeg, textop.RightLeg);
        }
        else
        {
            // 旧皮肤只加载头部 Top
            CreateDXItem(ref _topModel.Head, top.Head, textop.Head);
        }

        _switchModel = false;
    }
}
