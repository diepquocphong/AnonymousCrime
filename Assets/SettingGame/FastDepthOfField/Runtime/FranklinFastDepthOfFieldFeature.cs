using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FranklinGame.Rendering
{
    /// <summary>
    /// URP/RenderGraph adaptation of Fast Mobile Depth of Field. The source is
    /// blurred at quarter resolution and composited with the camera depth texture,
    /// so scene materials do not need the legacy package's custom alpha shaders.
    /// </summary>
    public sealed class FranklinFastDepthOfFieldFeature : ScriptableRendererFeature
    {
        public const int ModeOff = 0;
        public const int ModeNear = 1;
        public const int ModeFar = 2;

        private static int s_RuntimeMode;

        [SerializeField] private Shader m_Shader;
        [Tooltip("Distance behind Player where GẦN starts blurring.")]
        [SerializeField, Min(1f)] private float m_NearBlurDistance = 15f;
        [Tooltip("Distance behind Player where XA starts blurring.")]
        [SerializeField, Min(1f)] private float m_FarBlurDistance = 40f;
        [SerializeField, Min(0.1f)] private float m_BlurTransition = 10f;
        [SerializeField, Range(0.25f, 3f)] private float m_BlurRadius = 1.35f;
        [SerializeField, Range(0f, 1f)] private float m_Strength = 0.3f;

        private Material m_Material;
        private FastDepthOfFieldPass m_Pass;

        public static int RuntimeMode => s_RuntimeMode;
        public static bool RuntimeEnabled => s_RuntimeMode != ModeOff;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            s_RuntimeMode = ModeOff;
        }

        public static void SetRuntimeMode(int mode)
        {
            s_RuntimeMode = Mathf.Clamp(mode, ModeOff, ModeFar);
        }

        public override void Create()
        {
            CoreUtils.Destroy(this.m_Material);
            if (this.m_Shader == null)
            {
                this.m_Shader = Shader.Find(
                    "Hidden/Franklin/Fast Mobile Depth Of Field"
                );
            }

            if (this.m_Shader == null || !this.m_Shader.isSupported)
            {
                this.m_Pass = null;
                return;
            }

            this.m_Material = CoreUtils.CreateEngineMaterial(this.m_Shader);
            this.m_Pass = new FastDepthOfFieldPass(this.m_Material)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (this.m_Shader == null || !this.m_Shader.isSupported ||
                this.m_Pass == null || this.m_Material == null ||
                this.m_Material.passCount < 2 || !RuntimeEnabled)
            {
                return;
            }

            CameraData cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game ||
                cameraData.renderType != CameraRenderType.Base)
            {
                return;
            }

            bool focusNear = s_RuntimeMode == ModeNear;
            float playerEyeDepth = 0f;
            Transform player = ShortcutPlayer.Transform;
            if (player != null)
            {
                Vector3 playerViewPosition =
                    cameraData.camera.worldToCameraMatrix.MultiplyPoint(
                        player.position + Vector3.up
                    );
                playerEyeDepth = Mathf.Max(0f, -playerViewPosition.z);
            }

            // Blur is far-only. Anchoring the threshold behind Player keeps the
            // character, their feet and all nearby geometry fully sharp even
            // when the third-person camera changes distance or enters a vehicle.
            float blurStart = playerEyeDepth +
                (focusNear ? this.m_NearBlurDistance : this.m_FarBlurDistance);
            this.m_Pass.Setup(
                blurStart,
                this.m_BlurTransition,
                this.m_BlurRadius,
                this.m_Strength
            );
            renderer.EnqueuePass(this.m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            this.m_Pass = null;
            CoreUtils.Destroy(this.m_Material);
            this.m_Material = null;
        }

        private sealed class FastDepthOfFieldPass : ScriptableRenderPass
        {
            private static readonly int BlurTextureId =
                Shader.PropertyToID("_FranklinDofBlurTexture");
            private static readonly int CameraDepthTextureId =
                Shader.PropertyToID("_CameraDepthTexture");
            private static readonly int BlurRadiusId =
                Shader.PropertyToID("_FranklinDofBlurRadius");
            private static readonly int BlurStartId =
                Shader.PropertyToID("_FranklinDofBlurStart");
            private static readonly int BlurTransitionId =
                Shader.PropertyToID("_FranklinDofBlurTransition");
            private static readonly int StrengthId =
                Shader.PropertyToID("_FranklinDofStrength");

            private readonly Material m_Material;
            private float m_BlurStart;
            private float m_BlurTransition;
            private float m_BlurRadius;
            private float m_Strength;

            public FastDepthOfFieldPass(Material material)
            {
                this.m_Material = material;
                this.requiresIntermediateTexture = true;
                this.ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(
                float blurStart,
                float blurTransition,
                float blurRadius,
                float strength)
            {
                this.m_BlurStart = Mathf.Max(0.1f, blurStart);
                this.m_BlurTransition = Mathf.Max(0.1f, blurTransition);
                this.m_BlurRadius = Mathf.Clamp(blurRadius, 0.25f, 3f);
                this.m_Strength = Mathf.Clamp01(strength);
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData)
            {
                UniversalResourceData resources =
                    frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!source.IsValid() || !depth.IsValid()) return;

                TextureDesc fullDescriptor = source.GetDescriptor(renderGraph);
                fullDescriptor.name = "Franklin DOF Composite";
                fullDescriptor.clearBuffer = false;
                fullDescriptor.msaaSamples = MSAASamples.None;
                fullDescriptor.filterMode = FilterMode.Bilinear;
                fullDescriptor.wrapMode = TextureWrapMode.Clamp;

                TextureDesc blurDescriptor = fullDescriptor;
                blurDescriptor.width = Mathf.Max(1, blurDescriptor.width / 4);
                blurDescriptor.height = Mathf.Max(1, blurDescriptor.height / 4);
                blurDescriptor.name = "Franklin DOF Quarter A";

                TextureHandle blurA = renderGraph.CreateTexture(blurDescriptor);
                blurDescriptor.name = "Franklin DOF Quarter B";
                TextureHandle blurB = renderGraph.CreateTexture(blurDescriptor);
                TextureHandle destination = renderGraph.CreateTexture(fullDescriptor);

                using (IUnsafeRenderGraphBuilder builder =
                       renderGraph.AddUnsafePass<PassData>(
                           "Franklin Fast Mobile DOF",
                           out PassData passData))
                {
                    passData.material = this.m_Material;
                    passData.source = source;
                    passData.depth = depth;
                    passData.blurA = blurA;
                    passData.blurB = blurB;
                    passData.destination = destination;
                    passData.blurStart = this.m_BlurStart;
                    passData.blurTransition = this.m_BlurTransition;
                    passData.blurRadius = this.m_BlurRadius;
                    passData.strength = this.m_Strength;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(depth, AccessFlags.Read);
                    builder.UseTexture(blurA, AccessFlags.ReadWrite);
                    builder.UseTexture(blurB, AccessFlags.ReadWrite);
                    builder.UseTexture(destination, AccessFlags.WriteAll);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (
                        PassData data,
                        UnsafeGraphContext context) => ExecutePass(data, context));
                }

                resources.cameraColor = destination;
            }

            private static void ExecutePass(
                PassData data,
                UnsafeGraphContext context)
            {
                CommandBuffer commandBuffer =
                    CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                Vector4 scaleBias = new(1f, 1f, 0f, 0f);

                context.cmd.SetGlobalFloat(BlurRadiusId, data.blurRadius);
                context.cmd.SetRenderTarget(data.blurA);
                Blitter.BlitTexture(
                    commandBuffer,
                    data.source,
                    scaleBias,
                    data.material,
                    0
                );

                context.cmd.SetGlobalFloat(BlurRadiusId, data.blurRadius * 1.5f);
                context.cmd.SetRenderTarget(data.blurB);
                Blitter.BlitTexture(
                    commandBuffer,
                    data.blurA,
                    scaleBias,
                    data.material,
                    0
                );

                context.cmd.SetGlobalTexture(BlurTextureId, data.blurB);
                context.cmd.SetGlobalTexture(CameraDepthTextureId, data.depth);
                context.cmd.SetGlobalFloat(BlurStartId, data.blurStart);
                context.cmd.SetGlobalFloat(
                    BlurTransitionId,
                    data.blurTransition
                );
                context.cmd.SetGlobalFloat(StrengthId, data.strength);
                context.cmd.SetRenderTarget(data.destination);
                Blitter.BlitTexture(
                    commandBuffer,
                    data.source,
                    scaleBias,
                    data.material,
                    1
                );
            }

            private sealed class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle depth;
                public TextureHandle blurA;
                public TextureHandle blurB;
                public TextureHandle destination;
                public float blurStart;
                public float blurTransition;
                public float blurRadius;
                public float strength;
            }
        }
    }
}
