namespace MinecraftSkinRender.Vulkan;

/// <summary>
/// 方块模型
/// </summary>
public class CubeModelVulkan : CubeModel
{
    public const float Value = 0.5f;

    public static readonly float[] Vertices =
    [
        0.0f,  0.0f, -1.0f,
        0.0f,  0.0f, -1.0f,
        0.0f,  0.0f, -1.0f,
        0.0f,  0.0f, -1.0f,

        0.0f,  0.0f,  1.0f,
        0.0f,  0.0f,  1.0f,
        0.0f,  0.0f,  1.0f,
        0.0f,  0.0f,  1.0f,

        -1.0f,  0.0f,  0.0f,
        -1.0f,  0.0f,  0.0f,
        -1.0f,  0.0f,  0.0f,
        -1.0f,  0.0f,  0.0f,

        1.0f,  0.0f,  0.0f,
        1.0f,  0.0f,  0.0f,
        1.0f,  0.0f,  0.0f,
        1.0f,  0.0f,  0.0f,

        0.0f,  1.0f,  0.0f,
        0.0f,  1.0f,  0.0f,
        0.0f,  1.0f,  0.0f,
        0.0f,  1.0f,  0.0f,

        0.0f, -1.0f,  0.0f,
        0.0f, -1.0f,  0.0f,
        0.0f, -1.0f,  0.0f,
        0.0f, -1.0f,  0.0f,
    ];

    private static readonly float[] _cube =
    [
        Value, Value, -Value, /* Back. */
        Value, -Value, -Value,
        Value, Value, Value,
        Value, -Value, Value,
        -Value, Value, Value, /* Front. */
        -Value, -Value, Value,
        -Value, Value, -Value,
        -Value,- Value, -Value,
        Value, Value, -Value, /* Left. */
        Value, Value, Value,
        -Value, Value, -Value,
        -Value, Value, Value,
        Value, -Value, -Value, /* Right. */
        -Value, -Value, -Value,
        Value, -Value, Value,
        -Value, -Value, Value,
        Value, Value, Value, /* Top. */
        Value, -Value, Value,
        -Value, Value, Value,
        -Value, -Value, Value,
        -Value, Value, -Value, /* Bottom. */
        -Value, -Value, -Value,
        Value,-Value, -Value,
        Value, -Value, -Value,
    ];

    private static readonly ushort[] _cubeIndicies =
    [
       0, 1, 2, 2, 1, 3,
       //4, 5, 6, 4, 6, 7,
       //8, 9, 10, 8, 10, 11,
       //12, 13, 14, 12, 14, 15,
       //16, 17, 18, 16, 18, 19,
       //20, 21, 22, 20, 22, 23
    ];

    /// <summary>
    /// 获得一个方块X Y Z坐标
    /// </summary>
    /// <param name="multiplyX"></param>
    /// <param name="multiplyY"></param>
    /// <param name="multiplyZ"></param>
    /// <param name="addX"></param>
    /// <param name="addY"></param>
    /// <param name="addZ"></param>
    /// <param name="enlarge"></param>
    /// <returns></returns>
    public override float[] GetSquare(float multiplyX = 1.0f, float multiplyY = 1.0f, float multiplyZ = 1.0f,
        float addX = 0.0f, float addY = 0.0f, float addZ = 0.0f, float enlarge = 1.0f)
    {
        var temp = new float[_cube.Length];
        for (int a = 0; a < temp.Length; a++)
        {
            temp[a] = _cube[a] * enlarge;
            if (a % 3 == 0)
            {
                temp[a] = temp[a] * multiplyX + addX;
            }
            else if (a % 3 == 1)
            {
                temp[a] = temp[a] * multiplyY + addY;
            }
            else
            {
                temp[a] = temp[a] * multiplyZ + addZ;
            }
        }

        return temp;
    }

    /// <summary>
    /// 获得一个标准方块顶点顺序
    /// </summary>
    /// <param name="offset"></param>
    /// <returns></returns>
    public override ushort[] GetSquareIndicies(int offset = 0)
    {
        var temp = new ushort[36];
        temp[0] = 0;
        temp[1] = 1;
        temp[2] = 2;
        temp[3] = 2;
        temp[4] = 1;
        temp[5] = 3;
        for (int i = 1; i < 6; i++)
            for (int j = 0; j < 6; j++)
                temp[i * 6 + j] = (ushort)(temp[j] + i * 4);

        return temp;
    }
}
