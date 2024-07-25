using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[Serializable]
[PostProcess(typeof(CaviFreeRenderer), PostProcessEvent.AfterStack, "Custom/CaviFree")]
public sealed class CaviFree : PostProcessEffectSettings
{
    [Range(0f, 1f), Tooltip("Screen Space Curvature Ridge Effect.")]
    public FloatParameter ridge = new FloatParameter { value = 1f };

    [Range(0f, 1f), Tooltip("Screen Space Curvature Valley Effect.")]
    public FloatParameter valley = new FloatParameter { value = 1f };
}

public sealed class CaviFreeRenderer : PostProcessEffectRenderer<CaviFree>
{
    public override void Render(PostProcessRenderContext context)
    {
        var sheet = context.propertySheets.Get(Shader.Find("Hidden/Custom/CaviFree"));
        sheet.properties.SetFloat("_curvature_ridge", 0.5f / Mathf.Max(settings.ridge * settings.ridge, 1e-4f));
	    sheet.properties.SetFloat("_curvature_valley", 0.7f / Mathf.Max(settings.valley * settings.valley, 1e-4f));
        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
    }

    public override DepthTextureMode GetCameraFlags()
    {
        if (settings == null)
            return DepthTextureMode.None;

        return DepthTextureMode.DepthNormals;
    }
}
