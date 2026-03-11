using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Legacy + RenderGraph 호환 패스쓰루 배경 클리어 Feature.
/// Google의 TransparentBackgroundRendererFeature가 RenderGraph만 지원하여
/// Legacy 모드에서 작동하지 않는 문제를 해결합니다.
/// </summary>
public class PassthroughBackgroundRendererFeature : ScriptableRendererFeature
{
    private ClearBackgroundPass _pass;

    public override void Create()
    {
        _pass = new ClearBackgroundPass
        {
            renderPassEvent = RenderPassEvent.BeforeRendering
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_pass);
    }

    private class ClearBackgroundPass : ScriptableRenderPass
    {
        // Legacy 렌더 경로용 (현재 프로젝트에서 사용)
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("Clear Transparent Background");
            cmd.ClearRenderTarget(true, true, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

#if UNITY_6000_0_OR_NEWER
        // RenderGraph 경로용 (향후 호환)
        public override void RecordRenderGraph(
            UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph,
            ContextContainer frameData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                "Clear Transparent Background", out var passData))
            {
                builder.SetRenderFunc((PassData data, UnityEngine.Rendering.RenderGraphModule.RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(true, true, Color.clear);
                });
            }
        }

        private class PassData { }
#endif
    }
}
