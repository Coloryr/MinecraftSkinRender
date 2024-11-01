﻿namespace MinecraftSkinRender;

/// <summary>
/// 皮肤类型
/// </summary>
public enum SkinType
{
    /// <summary>
    /// 1.7旧版
    /// </summary>
    Old,
    /// <summary>
    /// 1.8新版
    /// </summary>
    New,
    /// <summary>
    /// 1.8新版纤细
    /// </summary>
    NewSlim,
    /// <summary>
    /// 未知的类型
    /// </summary>
    Unkonw
}

public enum KeyType
{
    None,
    Left,
    Right
}

public enum ErrorType
{
    UnknowSkinType, SkinNotFind
}

public enum StateType
{
    SkinReload
}

public enum MatrPartType
{ 
    Head, Body, LeftArm, RightArm, LeftLeg, RightLeg, Cape,
    Proj, View, Model
}