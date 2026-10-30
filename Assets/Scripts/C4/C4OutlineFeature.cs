using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace C4
{
    /// <summary>
    /// Renderer feature buat outline C4.
    /// Render semua renderer yang lagi di-hover ke mask, lalu bikin garis tipis
    /// screen-space dari pinggiran mask. Karena screen-space, outline tetap kelihatan
    /// di objek bevel maupun pas dilihat lurus dari depan.
    /// </summary>
    [DisallowMultipleRendererFeature("C4 Outline")]
    public class C4OutlineFeature : ScriptableRendererFeature
    {
        private C4OutlinePass _pass;

        public override void Create()
        {
            _pass?.Dispose();

            _pass = new C4OutlinePass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || !C4OutlineStatus.Visible)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;

            // Base camera aja, biar ga dobel pas ada camera stacking.
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
        }

        private sealed class C4OutlinePass : ScriptableRenderPass
        {
            private static readonly ProfilingSampler Profiling = new ProfilingSampler("C4 Outline");
            private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidthPixels");
            private static readonly int OutlineTexelSizeId = Shader.PropertyToID("_C4OutlineTexelSize");

            private RTHandle _maskHandle;
            private Material _maskMaterial;
            private Material _compositeMaterial;
            private bool _missingShaderLogged;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                EnsureMaterials();

                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;

                RenderingUtils.ReAllocateIfNeeded(
                    ref _maskHandle,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_C4OutlineMask");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!C4OutlineStatus.Visible)
                    return;

                EnsureMaterials();

                CameraData cameraData = renderingData.cameraData;
                if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                    return;

                if (_maskHandle == null || _maskHandle.rt == null ||
                    _maskMaterial == null || _compositeMaterial == null)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, Profiling))
                {
                    // 1. Render semua objek yang di-hover ke mask (putih = ada objek).
                    CoreUtils.SetRenderTarget(cmd, _maskHandle, ClearFlag.Color, Color.clear);

                    List<Renderer> targets = C4OutlineStatus.Renderers;
                    for (int i = 0; i < targets.Count; i++)
                    {
                        Renderer target = targets[i];
                        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy)
                            continue;

                        int subMeshCount = GetSubMeshCount(target);
                        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                            cmd.DrawRenderer(target, _maskMaterial, subMesh, 0);
                    }

                    // 2. Composite garis outline ke layar dari pinggiran mask.
                    _compositeMaterial.SetColor(OutlineColorId, C4OutlineStatus.Color);
                    _compositeMaterial.SetFloat(OutlineWidthId, Mathf.Max(0.5f, C4OutlineStatus.WidthPixels));

                    cmd.SetGlobalVector(
                        OutlineTexelSizeId,
                        new Vector4(
                            1f / _maskHandle.rt.width,
                            1f / _maskHandle.rt.height,
                            _maskHandle.rt.width,
                            _maskHandle.rt.height));

                    Blitter.BlitCameraTexture(
                        cmd,
                        _maskHandle,
                        cameraData.renderer.cameraColorTargetHandle,
                        RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.Store,
                        _compositeMaterial,
                        0);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                _maskHandle?.Release();
                _maskHandle = null;

                if (_maskMaterial != null)
                {
                    CoreUtils.Destroy(_maskMaterial);
                    _maskMaterial = null;
                }

                if (_compositeMaterial != null)
                {
                    CoreUtils.Destroy(_compositeMaterial);
                    _compositeMaterial = null;
                }
            }

            private void EnsureMaterials()
            {
                if (_maskMaterial == null)
                {
                    Shader maskShader = Shader.Find("Hidden/C4/C4OutlineMask");
                    if (maskShader == null)
                        maskShader = Resources.Load<Shader>("C4OutlineMask");

                    if (maskShader != null)
                        _maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
                }

                if (_compositeMaterial == null)
                {
                    Shader compositeShader = Shader.Find("Hidden/C4/C4OutlineComposite");
                    if (compositeShader == null)
                        compositeShader = Resources.Load<Shader>("C4OutlineComposite");

                    if (compositeShader != null)
                        _compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
                }

                if ((_maskMaterial == null || _compositeMaterial == null) && !_missingShaderLogged)
                {
                    _missingShaderLogged = true;
                    Debug.LogWarning("[C4OutlineFeature] Shader C4OutlineMask / C4OutlineComposite gak ketemu di Resources atau Shader registry.");
                }
            }

            private static int GetSubMeshCount(Renderer target)
            {
                Mesh mesh = null;

                if (target is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else
                {
                    MeshFilter filter = target.GetComponent<MeshFilter>();
                    if (filter != null)
                        mesh = filter.sharedMesh;
                }

                return mesh != null ? Mathf.Max(1, mesh.subMeshCount) : 1;
            }
        }
    }
}
