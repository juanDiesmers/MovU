using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ============================================================================
// MovUCalidadEditor.cs — Que dar Play no cambie el asset de URP del proyecto
// ============================================================================
// CalidadGrafica cambia en tiempo de ejecución el asset de URP activo
// (sombras, cascadas, SSAO, resolución interna). En el editor ese asset es el
// del proyecto, así que los cambios se quedarían al salir de Play.
//
// CalidadGrafica ya los devuelve en Application.quitting, pero si los scripts
// se recompilan con el juego corriendo se pierde lo que tenía guardado. Por eso
// aquí, del lado del editor, se toma una foto justo antes de entrar a Play
// (SessionState sobrevive a las recargas de dominio) y se devuelve al volver al
// modo de edición.
// ============================================================================

[InitializeOnLoad]
public static class MovUCalidadEditor
{
    private const string Clave = "MovU_CalidadEditor_";

    static MovUCalidadEditor()
    {
        EditorApplication.playModeStateChanged -= AlCambiar;
        EditorApplication.playModeStateChanged += AlCambiar;
    }

    private static void AlCambiar(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.ExitingEditMode) Foto();
        else if (estado == PlayModeStateChange.EnteredEditMode) Devolver();
    }

    private static UniversalRenderPipelineAsset Asset() =>
        GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

    private static ScriptableRendererFeature Ssao(UniversalRenderPipelineAsset urp)
    {
        var datos = urp.rendererDataList;
        for (int i = 0; i < datos.Length; i++)
        {
            if (datos[i] == null) continue;
            foreach (var r in datos[i].rendererFeatures)
                if (r != null && r.GetType().Name == "ScreenSpaceAmbientOcclusion") return r;
        }
        return null;
    }

    private static void Foto()
    {
        var urp = Asset();
        if (urp == null) return;
        SessionState.SetBool(Clave + "hay", true);
        SessionState.SetFloat(Clave + "escala", urp.renderScale);
        SessionState.SetFloat(Clave + "sombras", urp.shadowDistance);
        SessionState.SetInt(Clave + "cascadas", urp.shadowCascadeCount);
        SessionState.SetInt(Clave + "luces", urp.maxAdditionalLightsCount);
        SessionState.SetInt(Clave + "filtro", (int)urp.upscalingFilter);
        var ssao = Ssao(urp);
        SessionState.SetBool(Clave + "ssao", ssao == null || ssao.isActive);
        SessionState.SetInt(Clave + "mip", QualitySettings.globalTextureMipmapLimit);
        SessionState.SetInt(Clave + "aniso", (int)QualitySettings.anisotropicFiltering);
        SessionState.SetFloat(Clave + "lod", QualitySettings.lodBias);
    }

    private static void Devolver()
    {
        if (!SessionState.GetBool(Clave + "hay", false)) return;
        var urp = Asset();
        if (urp == null) return;

        urp.renderScale = SessionState.GetFloat(Clave + "escala", 1f);
        urp.shadowDistance = SessionState.GetFloat(Clave + "sombras", 50f);
        urp.shadowCascadeCount = SessionState.GetInt(Clave + "cascadas", 4);
        urp.maxAdditionalLightsCount = SessionState.GetInt(Clave + "luces", 4);
        urp.upscalingFilter = (UpscalingFilterSelection)SessionState.GetInt(Clave + "filtro", 0);
        var ssao = Ssao(urp);
        if (ssao != null) ssao.SetActive(SessionState.GetBool(Clave + "ssao", true));

        QualitySettings.globalTextureMipmapLimit = SessionState.GetInt(Clave + "mip", 0);
        QualitySettings.anisotropicFiltering = (AnisotropicFiltering)SessionState.GetInt(Clave + "aniso", 1);
        QualitySettings.lodBias = SessionState.GetFloat(Clave + "lod", 1f);
        SessionState.SetBool(Clave + "hay", false);
    }
}
