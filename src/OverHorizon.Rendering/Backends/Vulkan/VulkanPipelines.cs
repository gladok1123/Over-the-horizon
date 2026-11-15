using Silk.NET.Vulkan;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Builds every descriptor set layout, pipeline layout and graphics pipeline the renderer needs from the
/// precompiled SPIR-V modules: the opaque and translucent terrain passes, the blended world overlay passes
/// (one with and one without depth writes, for clouds and wireframes) and the screen-space HUD pass.
/// </summary>
internal sealed unsafe class VulkanPipelines : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanPipelines"/> class.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="renderPass">Render pass every pipeline is built against.</param>
    /// <param name="modules">Precompiled shader modules.</param>
    public VulkanPipelines(Vk vk, Device device, RenderPass renderPass, VulkanShaderModules modules)
    {
        _vk = vk;
        _device = device;

        ChunkSetLayout = CreateChunkSetLayout();
        OverlaySetLayout = CreateSamplerSetLayout(2);
        UiSetLayout = CreateSamplerSetLayout(3);

        ChunkLayout = CreatePipelineLayout(
            ChunkSetLayout,
            new PushConstantRange(ShaderStageFlags.VertexBit, 0, VulkanFrameUniforms.SectionPushSize));

        OverlayLayout = CreatePipelineLayout(
            OverlaySetLayout,
            new PushConstantRange(
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
                0,
                VulkanOverlayPushConstants.SizeInBytes));

        UiLayout = CreatePipelineLayout(
            UiSetLayout,
            new PushConstantRange(ShaderStageFlags.VertexBit, 0, 8));

        var chunkVertex = modules.Get("chunk.vert.spv");
        var chunkFragment = modules.Get("chunk.frag.spv");
        var overlayVertex = modules.Get("overlay.vert.spv");
        var overlayFragment = modules.Get("overlay.frag.spv");
        var uiVertex = modules.Get("ui.vert.spv");
        var uiFragment = modules.Get("ui.frag.spv");

        ChunkOpaque = CreatePipeline(
            renderPass,
            ChunkLayout,
            chunkVertex,
            chunkFragment,
            VulkanVertexFormats.Chunk(),
            depthTest: true,
            depthWrite: true,
            blend: false,
            cullBackFaces: true);

        ChunkTranslucent = CreatePipeline(
            renderPass,
            ChunkLayout,
            chunkVertex,
            chunkFragment,
            VulkanVertexFormats.Chunk(),
            depthTest: true,
            depthWrite: false,
            blend: true,
            cullBackFaces: false);

        Overlay = CreatePipeline(
            renderPass,
            OverlayLayout,
            overlayVertex,
            overlayFragment,
            VulkanVertexFormats.Overlay(),
            depthTest: true,
            depthWrite: true,
            blend: true,
            cullBackFaces: false);

        OverlayLines = CreatePipeline(
            renderPass,
            OverlayLayout,
            overlayVertex,
            overlayFragment,
            VulkanVertexFormats.Overlay(),
            depthTest: true,
            depthWrite: true,
            blend: true,
            cullBackFaces: false,
            topology: PrimitiveTopology.LineList);

        // The celestial dome sits behind everything: depth testing is off so the far plane can never clip it.
        OverlayNoDepth = CreatePipeline(
            renderPass,
            OverlayLayout,
            overlayVertex,
            overlayFragment,
            VulkanVertexFormats.Overlay(),
            depthTest: false,
            depthWrite: false,
            blend: true,
            cullBackFaces: false);

        OverlayNoDepthWrite = CreatePipeline(
            renderPass,
            OverlayLayout,
            overlayVertex,
            overlayFragment,
            VulkanVertexFormats.Overlay(),
            depthTest: true,
            depthWrite: false,
            blend: true,
            cullBackFaces: false);

        Ui = CreatePipeline(
            renderPass,
            UiLayout,
            uiVertex,
            uiFragment,
            VulkanVertexFormats.Ui(),
            depthTest: false,
            depthWrite: false,
            blend: true,
            cullBackFaces: false);
    }

    /// <summary>Gets the descriptor layout of the terrain pass (frame uniform buffer plus block atlas).</summary>
    public DescriptorSetLayout ChunkSetLayout { get; }

    /// <summary>Gets the descriptor layout of the overlay passes (block atlas plus entity atlas).</summary>
    public DescriptorSetLayout OverlaySetLayout { get; }

    /// <summary>Gets the descriptor layout of the HUD pass (font, item and GUI atlases).</summary>
    public DescriptorSetLayout UiSetLayout { get; }

    /// <summary>Gets the terrain pipeline layout.</summary>
    public PipelineLayout ChunkLayout { get; }

    /// <summary>Gets the overlay pipeline layout.</summary>
    public PipelineLayout OverlayLayout { get; }

    /// <summary>Gets the HUD pipeline layout.</summary>
    public PipelineLayout UiLayout { get; }

    /// <summary>Gets the opaque terrain pipeline.</summary>
    public Pipeline ChunkOpaque { get; }

    /// <summary>Gets the depth-sorted, blended translucent terrain pipeline.</summary>
    public Pipeline ChunkTranslucent { get; }

    /// <summary>Gets the blended world overlay pipeline that writes depth (mobs, drops, particles).</summary>
    public Pipeline Overlay { get; }

    /// <summary>Gets the blended overlay pipeline for selection wireframes (line-list topology).</summary>
    public Pipeline OverlayLines { get; }

    /// <summary>Gets the blended overlay pipeline for the celestial dome (no depth test, no depth write).</summary>
    public Pipeline OverlayNoDepth { get; }

    /// <summary>Gets the blended world overlay pipeline that leaves depth alone (clouds, selection wireframe).</summary>
    public Pipeline OverlayNoDepthWrite { get; }

    /// <summary>Gets the screen-space HUD pipeline.</summary>
    public Pipeline Ui { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _vk.DestroyPipeline(_device, ChunkOpaque, null);
        _vk.DestroyPipeline(_device, ChunkTranslucent, null);
        _vk.DestroyPipeline(_device, Overlay, null);
        _vk.DestroyPipeline(_device, OverlayLines, null);
        _vk.DestroyPipeline(_device, OverlayNoDepth, null);
        _vk.DestroyPipeline(_device, OverlayNoDepthWrite, null);
        _vk.DestroyPipeline(_device, Ui, null);
        _vk.DestroyPipelineLayout(_device, ChunkLayout, null);
        _vk.DestroyPipelineLayout(_device, OverlayLayout, null);
        _vk.DestroyPipelineLayout(_device, UiLayout, null);
        _vk.DestroyDescriptorSetLayout(_device, ChunkSetLayout, null);
        _vk.DestroyDescriptorSetLayout(_device, OverlaySetLayout, null);
        _vk.DestroyDescriptorSetLayout(_device, UiSetLayout, null);
    }

    private DescriptorSetLayout CreateChunkSetLayout()
    {
        var bindings = stackalloc DescriptorSetLayoutBinding[2];
        bindings[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
        };

        bindings[1] = new DescriptorSetLayoutBinding
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit,
        };

        var createInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 2,
            PBindings = bindings,
        };

        DescriptorSetLayout layout;
        VulkanResult.Ensure(
            _vk.CreateDescriptorSetLayout(_device, in createInfo, null, out layout),
            "vkCreateDescriptorSetLayout");

        return layout;
    }

    private DescriptorSetLayout CreateSamplerSetLayout(uint samplerCount)
    {
        var bindings = stackalloc DescriptorSetLayoutBinding[3];
        for (var i = 0u; i < samplerCount; i++)
        {
            bindings[i] = new DescriptorSetLayoutBinding
            {
                Binding = i + 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit,
            };
        }

        var createInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = samplerCount,
            PBindings = bindings,
        };

        DescriptorSetLayout layout;
        VulkanResult.Ensure(
            _vk.CreateDescriptorSetLayout(_device, in createInfo, null, out layout),
            "vkCreateDescriptorSetLayout");

        return layout;
    }

    private PipelineLayout CreatePipelineLayout(DescriptorSetLayout setLayout, PushConstantRange pushRange)
    {
        var setLayouts = stackalloc DescriptorSetLayout[1];
        setLayouts[0] = setLayout;

        var createInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = setLayouts,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushRange,
        };

        PipelineLayout layout;
        VulkanResult.Ensure(_vk.CreatePipelineLayout(_device, in createInfo, null, out layout), "vkCreatePipelineLayout");
        return layout;
    }

    private Pipeline CreatePipeline(
        RenderPass renderPass,
        PipelineLayout layout,
        ShaderModule vertex,
        ShaderModule fragment,
        VulkanVertexFormat vertexFormat,
        bool depthTest,
        bool depthWrite,
        bool blend,
        bool cullBackFaces,
        PrimitiveTopology topology = PrimitiveTopology.TriangleList)
    {
        var entryPoint = "main"u8;
        fixed (byte* entryPointPtr = entryPoint)
        {
            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertex,
                PName = entryPointPtr,
            };

            stages[1] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragment,
                PName = entryPointPtr,
            };

            fixed (VertexInputBindingDescription* bindingPtr = vertexFormat.Bindings)
            fixed (VertexInputAttributeDescription* attributePtr = vertexFormat.Attributes)
            {
                var vertexInput = new PipelineVertexInputStateCreateInfo
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = (uint)vertexFormat.Bindings.Length,
                    PVertexBindingDescriptions = bindingPtr,
                    VertexAttributeDescriptionCount = (uint)vertexFormat.Attributes.Length,
                    PVertexAttributeDescriptions = attributePtr,
                };

                var inputAssembly = new PipelineInputAssemblyStateCreateInfo
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = topology,
                    PrimitiveRestartEnable = false,
                };

                // The viewport and scissor follow the swapchain, so they are set dynamically at draw time.
                var dynamicStates = stackalloc DynamicState[2];
                dynamicStates[0] = DynamicState.Viewport;
                dynamicStates[1] = DynamicState.Scissor;

                var dynamicState = new PipelineDynamicStateCreateInfo
                {
                    SType = StructureType.PipelineDynamicStateCreateInfo,
                    DynamicStateCount = 2,
                    PDynamicStates = dynamicStates,
                };

                var viewportState = new PipelineViewportStateCreateInfo
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    ScissorCount = 1,
                };

                var rasterizer = new PipelineRasterizationStateCreateInfo
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    DepthClampEnable = false,
                    RasterizerDiscardEnable = false,
                    PolygonMode = PolygonMode.Fill,
                    LineWidth = 1.0f,
                    CullMode = cullBackFaces ? CullModeFlags.BackBit : CullModeFlags.None,
                    FrontFace = FrontFace.CounterClockwise,
                    DepthBiasEnable = false,
                };

                var multisampling = new PipelineMultisampleStateCreateInfo
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    SampleShadingEnable = false,
                    RasterizationSamples = SampleCountFlags.Count1Bit,
                };

                var colorBlendAttachment = new PipelineColorBlendAttachmentState
                {
                    ColorWriteMask = ColorComponentFlags.RBit
                        | ColorComponentFlags.GBit
                        | ColorComponentFlags.BBit
                        | ColorComponentFlags.ABit,
                    BlendEnable = blend,
                    SrcColorBlendFactor = BlendFactor.SrcAlpha,
                    DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = BlendOp.Add,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    AlphaBlendOp = BlendOp.Add,
                };

                var colorBlending = new PipelineColorBlendStateCreateInfo
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = false,
                    LogicOp = LogicOp.Copy,
                    AttachmentCount = 1,
                    PAttachments = &colorBlendAttachment,
                };

                var depthStencil = new PipelineDepthStencilStateCreateInfo
                {
                    SType = StructureType.PipelineDepthStencilStateCreateInfo,
                    DepthTestEnable = depthTest,
                    DepthWriteEnable = depthWrite,
                    DepthCompareOp = CompareOp.LessOrEqual,
                    DepthBoundsTestEnable = false,
                    StencilTestEnable = false,
                };

                var pipelineInfo = new GraphicsPipelineCreateInfo
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = 2,
                    PStages = stages,
                    PVertexInputState = &vertexInput,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &rasterizer,
                    PMultisampleState = &multisampling,
                    PDepthStencilState = &depthStencil,
                    PColorBlendState = &colorBlending,
                    PDynamicState = &dynamicState,
                    Layout = layout,
                    RenderPass = renderPass,
                    Subpass = 0,
                };

                Pipeline pipeline;
                VulkanResult.Ensure(
                    _vk.CreateGraphicsPipelines(_device, default, 1, &pipelineInfo, null, &pipeline),
                    "vkCreateGraphicsPipelines");

                return pipeline;
            }
        }
    }
}
