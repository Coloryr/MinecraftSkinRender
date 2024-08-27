﻿using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace MinecraftSkinRender.Vulkan;

public partial class SkinRenderVulkan : SkinRender
{
    private uint _width, _height;

    const int MAX_FRAMES_IN_FLIGHT = 2;

    public const bool EnableValidationLayers = true;

    private readonly string[] validationLayers =
    [
        "VK_LAYER_KHRONOS_validation"
    ];

    private readonly string[] deviceExtensions =
    [
        KhrSwapchain.ExtensionName
    ];

    private Vk vk;

    private Instance instance;

    private ExtDebugUtils? debugUtils;
    private DebugUtilsMessengerEXT debugMessenger;
    private KhrSurface? khrSurface;
    private SurfaceKHR surface;

    private PhysicalDevice physicalDevice;
    private Device device;

    private Queue graphicsQueue;
    private Queue presentQueue;

    private KhrSwapchain khrSwapChain;
    private SwapchainKHR swapChain;
    private Image[] swapChainImages;
    private Format swapChainImageFormat;
    private Extent2D swapChainExtent;
    private ImageView[] swapChainImageViews;
    private Framebuffer[] swapChainFramebuffers;

    private RenderPass renderPass;
    private DescriptorSetLayout descriptorSetLayout;
    private PipelineLayout pipelineLayout;
    private Pipeline graphicsPipeline;
    private Pipeline graphicsPipelineTop;

    private CommandPool commandPool;

    private Image depthImage;
    private DeviceMemory depthImageMemory;
    private ImageView depthImageView;

    private Image textureImage;
    private DeviceMemory textureImageMemory;
    private ImageView textureImageView;
    private Sampler textureSampler;

    private Semaphore[] imageAvailableSemaphores;
    private Semaphore[] renderFinishedSemaphores;
    private Fence[] inFlightFences;
    private Fence[] imagesInFlight;
    private int currentFrame = 0;

    private bool frameBufferResized = false;

    private SkinModel model;
    private SkinDraw draw;
    private CommandBuffer[]? commandBuffers;
    private UniformBufferObject ubo;

    private readonly IVkSurface ivk;

    public unsafe SkinRenderVulkan(IVkSurface ivk)
    {
        vk = Vk.GetApi();
        this.ivk = ivk;
    }

    public void VulkanInit()
    {
        _width = (uint)Width;
        _height = (uint)Height;

        CreateInstance();
        SetupDebugMessenger();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateSwapChain();
        CreateImageViews();
        CreateRenderPass();
        CreateDescriptorSetLayout();
        CreateGraphicsPipeline();
        CreateCommandPool();
        CreateDepthResources();
        CreateFramebuffers();
        CreateTextureImage();
        CreateTextureSampler();
        CreateModel();
        CreateVertexBuffer();
        CreateIndexBuffer();
        CreateUniformBuffers();
        CreateDescriptorPool();
        CreateDescriptorSets();
        CreateCommandBuffers();
        CreateSyncObjects();
    }

    private void RecreateSwapChain()
    {
        vk.DeviceWaitIdle(device);

        CleanUpSwapChain();

        CreateSwapChain();
        CreateImageViews();
        CreateRenderPass();
        CreateGraphicsPipeline();
        CreateDepthResources();
        CreateFramebuffers();
        CreateUniformBuffers();
        CreateDescriptorPool();
        CreateDescriptorSets();
        CreateCommandBuffers();

        imagesInFlight = new Fence[swapChainImages.Length];
    }

    public unsafe void VulkanRender(double time)
    {
        if (Width != _width || Height != _height)
        {
            _width = (uint)Width;
            _height = (uint)Height;
            frameBufferResized = true;
        }

        if (_enableAnimation)
        {
            _skina.Tick(time);
        }

        vk.WaitForFences(device, 1, ref inFlightFences[currentFrame], true, ulong.MaxValue);

        uint imageIndex = 0;
        var result = khrSwapChain!.AcquireNextImage(device, swapChain, ulong.MaxValue, 
            imageAvailableSemaphores![currentFrame], default, ref imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapChain();
            return;
        }
        else if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new Exception("failed to acquire swap chain image!");
        }

        UpdateUniformBuffer();

        if (imagesInFlight[imageIndex].Handle != default)
        {
            vk.WaitForFences(device, 1, ref imagesInFlight[imageIndex], true, ulong.MaxValue);
        }
        imagesInFlight[imageIndex] = inFlightFences[currentFrame];

        var waitSemaphores = stackalloc[] { imageAvailableSemaphores[currentFrame] };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        var signalSemaphores = stackalloc[] { renderFinishedSemaphores![currentFrame] };

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,
            SignalSemaphoreCount = 1,
            CommandBufferCount = 1,
            PSignalSemaphores = signalSemaphores,
        };

        vk.ResetFences(device, 1, ref inFlightFences[currentFrame]);

        DrawSkin(submitInfo, imageIndex);

        var swapChains = stackalloc[] { swapChain };
        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,

            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,

            SwapchainCount = 1,
            PSwapchains = swapChains,

            PImageIndices = &imageIndex
        };

        result = khrSwapChain.QueuePresent(presentQueue, ref presentInfo);

        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || frameBufferResized)
        {
            frameBufferResized = false;
            RecreateSwapChain();
        }
        else if (result != Result.Success)
        {
            throw new Exception("failed to present swap chain image!");
        }

        currentFrame = (currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
    }

    public void VulkanDeinit()
    { 
        
    }

    private unsafe void CreateSyncObjects()
    {
        imageAvailableSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
        renderFinishedSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
        inFlightFences = new Fence[MAX_FRAMES_IN_FLIGHT];
        imagesInFlight = new Fence[swapChainImages!.Length];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        for (var i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
        {
            if (vk.CreateSemaphore(device, ref semaphoreInfo, null, out imageAvailableSemaphores[i]) != Result.Success ||
                vk.CreateSemaphore(device, ref semaphoreInfo, null, out renderFinishedSemaphores[i]) != Result.Success ||
                vk.CreateFence(device, ref fenceInfo, null, out inFlightFences[i]) != Result.Success)
            {
                throw new Exception("failed to create synchronization objects for a frame!");
            }
        }
    }

    private unsafe void DrawSkin(SubmitInfo submitInfo, uint currentImage)
    {
        bool enable = _enableAnimation;
        var value = SkinType == SkinType.NewSlim ? 1.375f : 1.5f;

        SetUniformBuffer(draw.Head, currentImage, Matrix4x4.CreateTranslation(0, CubeModel.Value, 0) *
           Matrix4x4.CreateRotationZ((enable ? _skina.Head.X : HeadRotate.X) / 360) *
           Matrix4x4.CreateRotationX((enable ? _skina.Head.Y : HeadRotate.Y) / 360) *
           Matrix4x4.CreateRotationY((enable ? _skina.Head.Z : HeadRotate.Z) / 360) *
           Matrix4x4.CreateTranslation(0, CubeModel.Value * 1.5f, 0));
        SetUniformBuffer(draw.Body, currentImage, Matrix4x4.Identity);
        SetUniformBuffer(draw.LeftArm, currentImage, Matrix4x4.CreateTranslation(CubeModel.Value / 2, -(value * CubeModel.Value), 0) *
                Matrix4x4.CreateRotationZ((enable ? _skina.Arm.X : ArmRotate.X) / 360) *
                Matrix4x4.CreateRotationX((enable ? _skina.Arm.Y : ArmRotate.Y) / 360) *
                Matrix4x4.CreateTranslation(value * CubeModel.Value - CubeModel.Value / 2, value * CubeModel.Value, 0));
        SetUniformBuffer(draw.RightArm, currentImage, Matrix4x4.CreateTranslation(-CubeModel.Value / 2, -(value * CubeModel.Value), 0) *
                Matrix4x4.CreateRotationZ((enable ? -_skina.Arm.X : -ArmRotate.X) / 360) *
                Matrix4x4.CreateRotationX((enable ? -_skina.Arm.Y : -ArmRotate.Y) / 360) *
                Matrix4x4.CreateTranslation(
                    -value * CubeModel.Value + CubeModel.Value / 2, value * CubeModel.Value, 0));
        SetUniformBuffer(draw.LeftLeg, currentImage, Matrix4x4.CreateTranslation(0, -1.5f * CubeModel.Value, 0) *
               Matrix4x4.CreateRotationZ((enable ? _skina.Leg.X : LegRotate.X) / 360) *
               Matrix4x4.CreateRotationX((enable ? _skina.Leg.Y : LegRotate.Y) / 360) *
               Matrix4x4.CreateTranslation(CubeModel.Value * 0.5f, -CubeModel.Value * 1.5f, 0));
        SetUniformBuffer(draw.RightLeg, currentImage, Matrix4x4.CreateTranslation(0, -1.5f * CubeModel.Value, 0) *
               Matrix4x4.CreateRotationZ((enable ? -_skina.Leg.X : -LegRotate.X) / 360) *
               Matrix4x4.CreateRotationX((enable ? -_skina.Leg.Y : -LegRotate.Y) / 360) *
               Matrix4x4.CreateTranslation(-CubeModel.Value * 0.5f, -CubeModel.Value * 1.5f, 0));

        SetUniformBuffer(draw.TopHead, currentImage, Matrix4x4.CreateTranslation(0, CubeModel.Value, 0) *
           Matrix4x4.CreateRotationZ((enable ? _skina.Head.X : HeadRotate.X) / 360) *
           Matrix4x4.CreateRotationX((enable ? _skina.Head.Y : HeadRotate.Y) / 360) *
           Matrix4x4.CreateRotationY((enable ? _skina.Head.Z : HeadRotate.Z) / 360) *
           Matrix4x4.CreateTranslation(0, CubeModel.Value * 1.5f, 0));
        SetUniformBuffer(draw.TopBody, currentImage, Matrix4x4.Identity);
        SetUniformBuffer(draw.TopLeftArm, currentImage, Matrix4x4.CreateTranslation(CubeModel.Value / 2, -(value * CubeModel.Value), 0) *
                Matrix4x4.CreateRotationZ((enable ? _skina.Arm.X : ArmRotate.X) / 360) *
                Matrix4x4.CreateRotationX((enable ? _skina.Arm.Y : ArmRotate.Y) / 360) *
                Matrix4x4.CreateTranslation(value * CubeModel.Value - CubeModel.Value / 2, value * CubeModel.Value, 0));
        SetUniformBuffer(draw.TopRightArm, currentImage, Matrix4x4.CreateTranslation(-CubeModel.Value / 2, -(value * CubeModel.Value), 0) *
                Matrix4x4.CreateRotationZ((enable ? -_skina.Arm.X : -ArmRotate.X) / 360) *
                Matrix4x4.CreateRotationX((enable ? -_skina.Arm.Y : -ArmRotate.Y) / 360) *
                Matrix4x4.CreateTranslation(
                    -value * CubeModel.Value + CubeModel.Value / 2, value * CubeModel.Value, 0));
        SetUniformBuffer(draw.TopLeftLeg, currentImage, Matrix4x4.CreateTranslation(0, -1.5f * CubeModel.Value, 0) *
               Matrix4x4.CreateRotationZ((enable ? _skina.Leg.X : LegRotate.X) / 360) *
               Matrix4x4.CreateRotationX((enable ? _skina.Leg.Y : LegRotate.Y) / 360) *
               Matrix4x4.CreateTranslation(CubeModel.Value * 0.5f, -CubeModel.Value * 1.5f, 0));
        SetUniformBuffer(draw.TopRightLeg, currentImage, Matrix4x4.CreateTranslation(0, -1.5f * CubeModel.Value, 0) *
               Matrix4x4.CreateRotationZ((enable ? -_skina.Leg.X : -LegRotate.X) / 360) *
               Matrix4x4.CreateRotationX((enable ? -_skina.Leg.Y : -LegRotate.Y) / 360) *
               Matrix4x4.CreateTranslation(-CubeModel.Value * 0.5f, -CubeModel.Value * 1.5f, 0));

        SetUniformBuffer(draw.Cape, currentImage, Matrix4x4.CreateTranslation(0, -2f * CubeModel.Value, -CubeModel.Value * 0.1f) *
               Matrix4x4.CreateRotationX((float)(10.8 * Math.PI / 180)) *
               Matrix4x4.CreateTranslation(0, 1.6f * CubeModel.Value, -CubeModel.Value * 0.5f));

        var command1 = commandBuffers![currentImage] ;

        var info = submitInfo with
        {
            PCommandBuffers = &command1
        };

        if (vk.QueueSubmit(graphicsQueue, 1, ref info, inFlightFences[currentFrame]) != Result.Success)
        {
            throw new Exception("failed to submit draw command buffer!");
        }
    }

    private unsafe void SetUniformBuffer(SkinDrawPart part, uint currentImage, Matrix4x4 self)
    {
        ubo.self = self;
        void* data;
        vk.MapMemory(device, part.uniformBuffersMemory[currentImage], 0, (ulong)Unsafe.SizeOf<UniformBufferObject>(), 0, &data);
        new Span<UniformBufferObject>(data, 1)[0] = ubo;
        vk.UnmapMemory(device, part.uniformBuffersMemory[currentImage]);
    }

    private unsafe void UpdateUniformBuffer()
    {
        if (_rotXY.X != 0 || _rotXY.Y != 0)
        {
            _last *= Matrix4x4.CreateRotationX(_rotXY.X / 360)
                    * Matrix4x4.CreateRotationY(_rotXY.Y / 360);
            _rotXY.X = 0;
            _rotXY.Y = 0;
        }

        ubo = new()
        {
            model = Matrix4x4.CreateRotationY(Scalar.DegreesToRadians(0f)) * _last
            * Matrix4x4.CreateTranslation(new(_xy.X, _xy.Y, 0))
            * Matrix4x4.CreateScale(_dis),
            view = Matrix4x4.CreateLookAt(new(0, 0, 7), new(0, 0, 0), new(0, 1, 0)),
            proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 4), (float)_width / _height, 0.1f, 10.0f),
            lightColor = new(1.0f, 1.0f, 1.0f)
        };
        ubo.proj.M22 *= -1;
    }

    private Format FindSupportedFormat(IEnumerable<Format> candidates, ImageTiling tiling, FormatFeatureFlags features)
    {
        foreach (var format in candidates)
        {
            vk!.GetPhysicalDeviceFormatProperties(physicalDevice, format, out var props);

            if (tiling == ImageTiling.Linear && (props.LinearTilingFeatures & features) == features)
            {
                return format;
            }
            else if (tiling == ImageTiling.Optimal && (props.OptimalTilingFeatures & features) == features)
            {
                return format;
            }
        }

        throw new Exception("failed to find supported format!");
    }

    private unsafe void CreateDescriptorSetLayout()
    {
        DescriptorSetLayoutBinding uboLayoutBinding = new()
        {
            Binding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBuffer,
            PImmutableSamplers = null,
            StageFlags = ShaderStageFlags.VertexBit,
        };

        DescriptorSetLayoutBinding samplerLayoutBinding = new()
        {
            Binding = 1,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImmutableSamplers = null,
            StageFlags = ShaderStageFlags.FragmentBit,
        };

        var bindings = new DescriptorSetLayoutBinding[] { uboLayoutBinding, samplerLayoutBinding };

        fixed (DescriptorSetLayoutBinding* bindingsPtr = bindings)
        fixed (DescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
        {
            DescriptorSetLayoutCreateInfo layoutInfo = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = bindingsPtr,
            };

            if (vk!.CreateDescriptorSetLayout(device, ref layoutInfo, null, descriptorSetLayoutPtr) != Result.Success)
            {
                throw new Exception("failed to create descriptor set layout!");
            }
        }
    }

    private Format FindDepthFormat()
    {
        return FindSupportedFormat([Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint], ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachmentBit);
    }

    private unsafe void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = swapChainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr,
        };

        AttachmentDescription depthAttachment = new()
        {
            Format = FindDepthFormat(),
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal,
        };

        AttachmentReference depthAttachmentRef = new()
        {
            Attachment = 1,
            Layout = ImageLayout.DepthStencilAttachmentOptimal,
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
            PDepthStencilAttachment = &depthAttachmentRef,
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
        };

        var attachments = new[] { colorAttachment, depthAttachment };

        fixed (AttachmentDescription* attachmentsPtr = attachments)
        {
            RenderPassCreateInfo renderPassInfo = new()
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = attachmentsPtr,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency,
            };

            if (vk!.CreateRenderPass(device, renderPassInfo, null, out renderPass) != Result.Success)
            {
                throw new Exception("failed to create render pass!");
            }
        }
    }

    private unsafe void SetupDebugMessenger()
    {
        if (!EnableValidationLayers) return;

        //TryGetInstanceExtension equivilant to method CreateDebugUtilsMessengerEXT from original tutorial.
        if (!vk!.TryGetInstanceExtension(instance, out debugUtils)) return;

        DebugUtilsMessengerCreateInfoEXT createInfo = new();
        PopulateDebugMessengerCreateInfo(ref createInfo);

        if (debugUtils!.CreateDebugUtilsMessenger(instance, in createInfo, null, out debugMessenger) != Result.Success)
        {
            throw new Exception("failed to set up debug messenger!");
        }
    }

    private unsafe bool CheckValidationLayerSupport()
    {
        uint layerCount = 0;
        vk.EnumerateInstanceLayerProperties(ref layerCount, null);
        var availableLayers = new LayerProperties[layerCount];
        fixed (LayerProperties* availableLayersPtr = availableLayers)
        {
            vk!.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
        }

        var availableLayerNames = availableLayers.Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName)).ToHashSet();

        return validationLayers.All(availableLayerNames.Contains);
    }

    private unsafe void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
        createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
    }

    private unsafe uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        Console.WriteLine($"validation layer:" + Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));

        return Vk.False;
    }
}