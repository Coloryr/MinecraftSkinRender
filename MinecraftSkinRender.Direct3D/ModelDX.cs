namespace MinecraftSkinRender.Direct3D;

internal record ModelDX : IDisposable
{
    public DXItem Head = new();
    public DXItem Body = new();
    public DXItem LeftArm = new();
    public DXItem RightArm = new();
    public DXItem LeftLeg = new();
    public DXItem RightLeg = new();
    public DXItem Cape = new();

    public void Dispose()
    {
        Head.Dispose();
        Body.Dispose();
        LeftArm.Dispose();
        RightArm.Dispose();
        LeftLeg.Dispose();
        RightLeg.Dispose();
        Cape.Dispose();
    }
}
