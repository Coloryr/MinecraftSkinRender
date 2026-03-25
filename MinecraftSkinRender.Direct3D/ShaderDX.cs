using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace MinecraftSkinRender.Direct3D;

public partial class SkinRenderDX11
{
    private ComPtr<ID3D11VertexShader> vertexShader = default;
    private ComPtr<ID3D11PixelShader> pixelShader = default;
    private ComPtr<ID3D11InputLayout> inputLayout = default;
    private ComPtr<ID3D11Buffer> constantBuffer;

    [StructLayout(LayoutKind.Sequential)]
    internal struct ModelConstantBuffer
    {
        public Matrix4x4 Model;
        public Matrix4x4 Projection;
        public Matrix4x4 View;
        public Matrix4x4 Self;
    }

    private const string ShaderSource = @"
    struct VS_INPUT {
        float3 position : POSITION;
        float2 texCoord : TEXCOORD;
        float3 normal : NORMAL;
    };

    struct PS_INPUT {
        float4 position : SV_POSITION;
        float2 texCoord : TEXCOORD;
        float3 normal : NORMAL;
        float3 fragPos : POSITION;
    };

    cbuffer ConstantBuffer : register(b0) {
        matrix u_model;
        matrix u_projection;
        matrix u_view;
        matrix u_self;
    };

    PS_INPUT vs_main(VS_INPUT input) {
        PS_INPUT output;
        
        float4x4 worldMatrix = mul(u_self, u_model);
        float4 worldPos = mul(float4(input.position, 1.0f), worldMatrix);
        
        output.fragPos = worldPos.xyz;
        output.normal = normalize(mul(input.normal, (float3x3)u_model));
        
        float4x4 viewProj = mul(u_view, u_projection);
        output.position = mul(worldPos, viewProj);
        
        output.texCoord = input.texCoord;
        return output;
    }

    Texture2D texture0 : register(t0);
    SamplerState sampler0 : register(s0);

    float4 ps_main(PS_INPUT input) : SV_Target {
        float3 lightColor = float3(1.0, 1.0, 1.0);
        float ambientStrength = 0.15;
        float3 lightPos = float3(0, 1, 5);
        
        float3 ambient = ambientStrength * lightColor;
        float3 norm = normalize(input.normal);
        float3 lightDir = normalize(lightPos - input.fragPos);
        float diff = max(dot(norm, lightDir), 0.0);
        float3 diffuse = diff * lightColor;
        
        float3 result = (ambient + diffuse);
        float4 texColor = texture0.Sample(sampler0, input.texCoord);
        return texColor * float4(result, 1.0);
    }
    ";

    private unsafe void InitShader()
    {
        var compiler = D3DCompiler.GetApi();

        var shaderBytes = Encoding.ASCII.GetBytes(ShaderSource);

        ComPtr<ID3D10Blob> vertexCode = default;
        ComPtr<ID3D10Blob> vertexErrors = default;
        HResult hr = compiler.Compile
        (
            in shaderBytes[0],
            (nuint)shaderBytes.Length,
            nameof(ShaderSource),
            null,
            ref Unsafe.NullRef<ID3DInclude>(),
            "vs_main",
            "vs_5_0",
            0,
            0,
            ref vertexCode,
            ref vertexErrors
        );

        // Check for compilation errors.
        if (hr.IsFailure)
        {
            if (vertexErrors.Handle is not null)
            {
                Console.WriteLine(SilkMarshal.PtrToString((nint)vertexErrors.GetBufferPointer()));
            }

            hr.Throw();
        }

        // Compile pixel shader.
        ComPtr<ID3D10Blob> pixelCode = default;
        ComPtr<ID3D10Blob> pixelErrors = default;
        hr = compiler.Compile
        (
            in shaderBytes[0],
            (nuint)shaderBytes.Length,
            nameof(ShaderSource),
            null,
            ref Unsafe.NullRef<ID3DInclude>(),
            "ps_main",
            "ps_5_0",
            0,
            0,
            ref pixelCode,
            ref pixelErrors
        );

        // Check for compilation errors.
        if (hr.IsFailure)
        {
            if (pixelErrors.Handle is not null)
            {
                Console.WriteLine(SilkMarshal.PtrToString((nint)pixelErrors.GetBufferPointer()));
            }

            hr.Throw();
        }

        // Create vertex shader.
        SilkMarshal.ThrowHResult
        (
            _device.CreateVertexShader
            (
                vertexCode.GetBufferPointer(),
                vertexCode.GetBufferSize(),
                ref Unsafe.NullRef<ID3D11ClassLinkage>(),
                ref vertexShader
            )
        );

        // Create pixel shader.
        SilkMarshal.ThrowHResult
        (
            _device.CreatePixelShader
            (
                pixelCode.GetBufferPointer(),
                pixelCode.GetBufferSize(),
                ref Unsafe.NullRef<ID3D11ClassLinkage>(),
                ref pixelShader
            )
        );

        // Describe the layout of the input data for the shader.
        fixed (byte* pPosName = SilkMarshal.StringToMemory("POSITION"))
        fixed (byte* pTexName = SilkMarshal.StringToMemory("TEXCOORD"))
        fixed (byte* pNormName = SilkMarshal.StringToMemory("NORMAL"))
        {
            var inputElements = new InputElementDesc[]
            {
                new()
                {
                    SemanticName = pPosName,
                    SemanticIndex = 0,
                    Format = Format.FormatR32G32B32Float,
                    InputSlot = 0,
                    AlignedByteOffset = 0,
                    InputSlotClass = InputClassification.PerVertexData,
                    InstanceDataStepRate = 0
                },
                new()
                {
                    SemanticName = pTexName,
                    SemanticIndex = 0, // TEXCOORD0
                    Format = Format.FormatR32G32Float,
                    InputSlot = 0,
                    AlignedByteOffset = 12, // AUTO
                    InputSlotClass = InputClassification.PerVertexData,
                    InstanceDataStepRate = 0
                },
                new()
                {
                    SemanticName = pNormName,
                    SemanticIndex = 0, // TEXCOORD0
                    Format = Format.FormatR32G32B32Float,
                    InputSlot = 0,
                    AlignedByteOffset = 20, // AUTO
                    InputSlotClass = InputClassification.PerVertexData,
                    InstanceDataStepRate = 0
                }
            };

            SilkMarshal.ThrowHResult
            (
                _device.CreateInputLayout
                (
                    in inputElements[0],
                    (uint)inputElements.Length,
                    vertexCode.GetBufferPointer(),
                    vertexCode.GetBufferSize(),
                    ref inputLayout
                )
            );
        }

        var cbDesc = new BufferDesc
        {
            ByteWidth = (uint)((Marshal.SizeOf<ModelConstantBuffer>() + 15) / 16 * 16),
            Usage = Usage.Dynamic,
            BindFlags = (uint)BindFlag.ConstantBuffer,
            CPUAccessFlags = (uint)CpuAccessFlag.Write
        };
        _device.CreateBuffer(in cbDesc, null, ref constantBuffer);

        // Clean up any resources.
        vertexCode.Dispose();
        vertexErrors.Dispose();
        pixelCode.Dispose();
        pixelErrors.Dispose();

        compiler.Dispose();
    }

    private void DeleteShader()
    {
        vertexShader.Dispose();
        pixelShader.Dispose();
        inputLayout.Dispose();
        constantBuffer.Dispose();
    }
}
