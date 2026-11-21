using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ============================================================================
// CalidadGrafica.cs — Ajustes de gráficos para equipos de pocos recursos
// ============================================================================
// Tres niveles (Bajo, Medio, Alto) que se eligen en Opciones > Gráficos y se
// guardan con PlayerPrefs. La primera vez que se abre el juego se elige uno
// solo según la tarjeta gráfica (las integradas de Intel o AMD van a Bajo).
//
// Qué cambia cada nivel:
//
//                         Bajo          Medio         Alto
//   Resolución interna    70 % (FSR)    100 %         100 %
//   Sombras del sol       no            30 m, 2 casc. 50 m, 4 casc.
//   SSAO                  no            no            sí
//   Antialiasing          no            FXAA          SMAA
//   Texturas              media res.    completas     completas
//   Filtro anisotrópico   no            sí            forzado
//
// La resolución interna se puede afinar aparte con su barra (50-100 %).
//
// Se toca el asset de URP que esté activo. En el EDITOR eso cambiaría el asset
// del proyecto, así que se guardan los valores originales y se devuelven al
// salir de Play (Application.quitting). En el ejecutable no hace falta.
// ============================================================================

public enum NivelGrafico { Bajo = 0, Medio = 1, Alto = 2 }

public static class CalidadGrafica
{
    public const float EscalaMin = 0.5f, EscalaMax = 1f;

    private const string NivelKey = "MovU_Opt_Calidad";
    private const string EscalaKey = "MovU_Opt_Escala";
    private const string FpsKey = "MovU_Opt_MostrarFps";

    // ------------------------------------------------------------------
    // Preferencias guardadas
    // ------------------------------------------------------------------
    public static NivelGrafico Nivel
    {
        get
        {
            if (!PlayerPrefs.HasKey(NivelKey)) return Detectar();
            return (NivelGrafico)Mathf.Clamp(PlayerPrefs.GetInt(NivelKey), 0, 2);
        }
    }

    /// <summary>Resolución interna (render scale de URP), de 0,5 a 1.</summary>
    public static float Escala
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(EscalaKey, EscalaPorDefecto(Nivel)), EscalaMin, EscalaMax);
        set
        {
            PlayerPrefs.SetFloat(EscalaKey, Mathf.Clamp(value, EscalaMin, EscalaMax));
            AplicarEscala();
        }
    }

    public static bool MostrarFps
    {
        get => PlayerPrefs.GetInt(FpsKey, 0) == 1;
        set => PlayerPrefs.SetInt(FpsKey, value ? 1 : 0);
    }

    public static float EscalaPorDefecto(NivelGrafico n) => n == NivelGrafico.Bajo ? 0.7f : 1f;

    public static string Nombre(NivelGrafico n) =>
        n == NivelGrafico.Bajo ? "Bajo" : n == NivelGrafico.Medio ? "Medio" : "Alto";

    public static string Descripcion(NivelGrafico n)
    {
        switch (n)
        {
            case NivelGrafico.Bajo:
                return "Para portátiles y gráficas integradas: sin sombras ni oclusión ambiental, resolución interna al 70 %.";
            case NivelGrafico.Medio:
                return "Equilibrado: sombras cercanas, suavizado FXAA, sin oclusión ambiental.";
            default:
                return "Todo activado: sombras largas, oclusión ambiental (SSAO) y suavizado SMAA.";
        }
    }

    /// <summary>Cambia de nivel y devuelve la resolución interna a la de ese nivel.</summary>
    public static void ElegirNivel(NivelGrafico n)
    {
        PlayerPrefs.SetInt(NivelKey, (int)n);
        PlayerPrefs.SetFloat(EscalaKey, EscalaPorDefecto(n));
        Aplicar();
    }

    /// <summary>
    /// Nivel sugerido para este equipo. Las gráficas integradas y las de poca
    /// memoria van a Bajo; las de hasta 4 GB a Medio; el resto a Alto.
    /// </summary>
    public static NivelGrafico Detectar()
    {
        string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
        int vram = SystemInfo.graphicsMemorySize;          // MB
        int ram = SystemInfo.systemMemorySize;             // MB

        bool integrada =
            (gpu.Contains("intel") && !gpu.Contains("arc")) ||
            gpu.Contains("uhd") || gpu.Contains("iris") ||
            gpu.Contains("vega") || gpu.Contains("radeon(tm) graphics") || gpu.Contains("radeon graphics") ||
            gpu.Contains("microsoft basic") || gpu.Contains("llvmpipe") || gpu.Contains("swiftshader");

        if (integrada || vram < 2000 || ram < 6000) return NivelGrafico.Bajo;
        if (vram < 4000) return NivelGrafico.Medio;
        return NivelGrafico.Alto;
    }

    // ------------------------------------------------------------------
    // Aplicar
    // ------------------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AlArrancar()
    {
        Aplicar();
    }

    public static void Aplicar()
    {
        GuardarOriginales();
        NivelGrafico n = Nivel;

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            switch (n)
            {
                case NivelGrafico.Bajo:
                    urp.shadowDistance = 0f;          // sin sombras del sol
                    urp.shadowCascadeCount = 1;
                    urp.maxAdditionalLightsCount = 2;
                    break;
                case NivelGrafico.Medio:
                    urp.shadowDistance = Mathf.Min(30f, original.sombras);
                    urp.shadowCascadeCount = 2;
                    urp.maxAdditionalLightsCount = Mathf.Min(4, original.lucesExtra);
                    break;
                default:
                    urp.shadowDistance = original.sombras;
                    urp.shadowCascadeCount = original.cascadas;
                    urp.maxAdditionalLightsCount = original.lucesExtra;
                    break;
            }
            ActivarSsao(urp, n == NivelGrafico.Alto && original.ssao);
        }

        QualitySettings.globalTextureMipmapLimit = n == NivelGrafico.Bajo ? 1 : 0;
        QualitySettings.anisotropicFiltering = n == NivelGrafico.Bajo ? AnisotropicFiltering.Disable
                                             : n == NivelGrafico.Medio ? AnisotropicFiltering.Enable
                                             : AnisotropicFiltering.ForceEnable;
        QualitySettings.lodBias = n == NivelGrafico.Bajo ? 0.7f : 1f;

        AplicarEscala();

        Camera[] camaras = Camera.allCameras;
        for (int i = 0; i < camaras.Length; i++) AplicarACamara(camaras[i]);
    }

    /// <summary>Suavizado y sombras de una cámara según el nivel (para cámaras creadas después).</summary>
    public static void AplicarACamara(Camera camara)
    {
        if (camara == null) return;
        var datos = camara.GetUniversalAdditionalCameraData();
        if (datos == null) return;

        NivelGrafico n = Nivel;
        datos.antialiasing = n == NivelGrafico.Bajo ? AntialiasingMode.None
                           : n == NivelGrafico.Medio ? AntialiasingMode.FastApproximateAntialiasing
                           : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        datos.antialiasingQuality = AntialiasingQuality.Medium;
        datos.renderShadows = n != NivelGrafico.Bajo;
    }

    private static void AplicarEscala()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        GuardarOriginales();

        float escala = Escala;
        urp.renderScale = escala;
        // Por debajo del 100 % se reescala con FSR, que conserva mejor los bordes
        // que el bilineal.
        urp.upscalingFilter = escala < 0.99f ? UpscalingFilterSelection.FSR : original.filtro;
    }

    private static void ActivarSsao(UniversalRenderPipelineAsset urp, bool activo)
    {
        var datos = urp.rendererDataList;
        for (int i = 0; i < datos.Length; i++)
        {
            if (datos[i] == null) continue;
            var rasgos = datos[i].rendererFeatures;
            for (int j = 0; j < rasgos.Count; j++)
            {
                if (rasgos[j] != null && rasgos[j].GetType().Name == "ScreenSpaceAmbientOcclusion")
                    rasgos[j].SetActive(activo);
            }
        }
    }

    private static bool LeerSsao(UniversalRenderPipelineAsset urp)
    {
        var datos = urp.rendererDataList;
        for (int i = 0; i < datos.Length; i++)
        {
            if (datos[i] == null) continue;
            var rasgos = datos[i].rendererFeatures;
            for (int j = 0; j < rasgos.Count; j++)
            {
                if (rasgos[j] != null && rasgos[j].GetType().Name == "ScreenSpaceAmbientOcclusion")
                    return rasgos[j].isActive;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Valores originales del proyecto (para el nivel Alto y para devolverlos
    // al asset al salir de Play dentro del editor).
    // ------------------------------------------------------------------
    private struct Originales
    {
        public float escala, sombras;
        public int cascadas, lucesExtra, mipmaps;
        public bool ssao;
        public UpscalingFilterSelection filtro;
        public AnisotropicFiltering aniso;
        public float lodBias;
    }

    private static Originales original;
    private static UniversalRenderPipelineAsset assetGuardado;
    private static bool guardados;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstaticos()
    {
        guardados = false;
        assetGuardado = null;
        Application.quitting -= Restaurar;
    }

    private static void GuardarOriginales()
    {
        if (guardados) return;
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        original.mipmaps = QualitySettings.globalTextureMipmapLimit;
        original.aniso = QualitySettings.anisotropicFiltering;
        original.lodBias = QualitySettings.lodBias;
        if (urp != null)
        {
            original.escala = urp.renderScale;
            original.sombras = urp.shadowDistance;
            original.cascadas = urp.shadowCascadeCount;
            original.lucesExtra = urp.maxAdditionalLightsCount;
            original.filtro = urp.upscalingFilter;
            original.ssao = LeerSsao(urp);
        }
        else
        {
            original.escala = 1f;
            original.sombras = 50f;
            original.cascadas = 4;
            original.lucesExtra = 4;
            original.ssao = true;
        }
        assetGuardado = urp;
        guardados = true;
        Application.quitting -= Restaurar;
        Application.quitting += Restaurar;
    }

    /// <summary>Devuelve el asset de URP y QualitySettings a como estaban.</summary>
    public static void Restaurar()
    {
        if (!guardados) return;
        QualitySettings.globalTextureMipmapLimit = original.mipmaps;
        QualitySettings.anisotropicFiltering = original.aniso;
        QualitySettings.lodBias = original.lodBias;

        UniversalRenderPipelineAsset urp = assetGuardado;
        if (urp != null)
        {
            urp.renderScale = original.escala;
            urp.shadowDistance = original.sombras;
            urp.shadowCascadeCount = original.cascadas;
            urp.maxAdditionalLightsCount = original.lucesExtra;
            urp.upscalingFilter = original.filtro;
            ActivarSsao(urp, original.ssao);
        }
        guardados = false;
    }
}
