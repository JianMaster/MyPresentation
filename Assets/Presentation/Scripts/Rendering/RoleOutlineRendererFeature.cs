using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class RoleOutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask roleLayerMask;
        public Color outlineColor = Color.black;
        [Range(1f, 8f)] public float outlineWidth = 2f;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    [SerializeField] private Settings _settings = new Settings();

    private Material _maskMaterial;
    private Material _outlineMaterial;
    private MaskPass _maskPass;
    private OutlinePass _outlinePass;

    public class TextureData : ContextItem
    {
        public TextureHandle maskTexture = TextureHandle.nullHandle;

        public override void Reset()
        {
            maskTexture = TextureHandle.nullHandle;
        }
    }

    public override void Create()
    {
        Dispose(true);
        _maskMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/RoleOutlineMask"));
        _outlineMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/RoleOutlineScreen"));

        _maskPass = new MaskPass(_settings, _maskMaterial);
        _outlinePass = new OutlinePass(_settings, _outlineMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_maskMaterial == null || _outlineMaterial == null) return;

        _maskPass.renderPassEvent = _settings.renderPassEvent;
        _outlinePass.renderPassEvent = _settings.renderPassEvent + 1;

        renderer.EnqueuePass(_maskPass);
        renderer.EnqueuePass(_outlinePass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_maskMaterial);
        CoreUtils.Destroy(_outlineMaterial);
    }

    private class MaskPass : ScriptableRenderPass
    {
        private static readonly int MaskTextureId = Shader.PropertyToID("_RoleOutlineMaskTexture");
        private static readonly ShaderTagId[] ShaderTags =
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("UniversalGBuffer"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        private readonly Settings _settings;
        private readonly Material _maskMaterial;
        private readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>(ShaderTags);

        private class PassData
        {
            public RendererListHandle rendererList;
        }

        public MaskPass(Settings settings, Material maskMaterial)
        {
            _settings = settings;
            _maskMaterial = maskMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureData textureData = frameData.GetOrCreate<TextureData>();

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.colorFormat = RenderTextureFormat.R8;

            TextureHandle maskTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                descriptor,
                "_RoleOutlineMaskTexture",
                true,
                FilterMode.Point,
                TextureWrapMode.Clamp
            );
            textureData.maskTexture = maskTexture;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Role Outline Mask", out var passData))
            {
                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, _settings.roleLayerMask);
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    _shaderTagIds,
                    renderingData,
                    cameraData,
                    lightData,
                    cameraData.defaultOpaqueSortFlags
                );
                drawingSettings.overrideMaterial = _maskMaterial;
                drawingSettings.perObjectData = PerObjectData.None;

                RendererListParams rendererListParams = new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    filteringSettings
                );
                rendererListParams.filteringSettings.batchLayerMask = uint.MaxValue;

                passData.rendererList = renderGraph.CreateRendererList(rendererListParams);

                builder.UseRendererList(passData.rendererList);
                builder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
                if (resourceData.activeDepthTexture.IsValid())
                {
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                }
                builder.SetGlobalTextureAfterPass(maskTexture, MaskTextureId);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(false, true, Color.clear);
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }
        }

    }

    private class OutlinePass : ScriptableRenderPass
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private readonly Settings _settings;
        private readonly Material _outlineMaterial;

        public OutlinePass(Settings settings, Material outlineMaterial)
        {
            _settings = settings;
            _outlineMaterial = outlineMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            TextureData textureData = frameData.Get<TextureData>();

            if (!resourceData.activeColorTexture.IsValid() || !textureData.maskTexture.IsValid()) return;

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            TextureHandle tempColor = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                descriptor,
                "_RoleOutlineTemporaryColorTexture",
                false,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp
            );

            _outlineMaterial.SetColor(OutlineColorId, _settings.outlineColor);
            _outlineMaterial.SetFloat(OutlineWidthId, _settings.outlineWidth);

            renderGraph.AddBlitPass(
                resourceData.activeColorTexture,
                tempColor,
                Vector2.one,
                Vector2.zero,
                passName: "Role Outline Copy Color"
            );

            RenderGraphUtils.BlitMaterialParameters parameters = new(
                tempColor,
                resourceData.activeColorTexture,
                _outlineMaterial,
                0
            );
            renderGraph.AddBlitPass(parameters, "Role Outline Composite");
        }

    }
}
