using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// VitrinaMenu.cs — El edificio girando detrás del menú principal
// ============================================================================
// Mientras el menú principal está en pantalla, el edificio se ve desde afuera,
// completo (los 11 pisos), con una cámara que le da la vuelta despacio. Al
// pulsar Comenzar todo vuelve a como estaba: un solo piso cargado, la cámara
// del jugador, la niebla de interior y el HUD.
//
// Cómo lo hace sin tocar el resto del juego:
//   - FloorManager.MostrarTodosLosPisos(true) enciende la geometría de todos
//     los pisos (no su contenido: NPC, puertas y rótulos siguen solo en el piso
//     del jugador).
//   - Una cámara propia, por encima de la del jugador. A la del jugador se le
//     deja la máscara de capas vacía (no dibuja nada), pero sigue activa para
//     que Camera.main siga respondiendo a los demás scripts.
//   - La niebla de interior (18-70 m) taparía el edificio a 150 m: se aleja
//     mientras dura la vitrina y se devuelve después.
//   - El HUD se oculta.
// El giro usa el tiempo real, así que no depende de Time.timeScale.
// ============================================================================

public class VitrinaMenu : MonoBehaviour
{
    private const float GradosPorSegundo = 5f;
    private const float Campo = 32f;               // FOV: lente larga, se ve más elegante
    private const float CorrimientoLateral = 11f;  // grados: deja el edificio a la derecha del menú

    public bool Mostrando { get; private set; }

    private Camera camara;
    private Camera camaraDelJugador;
    private int mascaraDelJugador;

    private bool niebla;
    private float nieblaInicio, nieblaFin;

    private readonly List<Canvas> lienzosOcultos = new List<Canvas>();

    private Vector3 centro;
    private float radio, altura, azimut;
    private bool medido;

    public void Mostrar()
    {
        if (Mostrando) return;
        FloorManager pisos = FloorManager.Instance;
        if (pisos == null) return;

        pisos.MostrarTodosLosPisos(true);
        Medir(pisos);

        // --- Cámara -----------------------------------------------------
        camaraDelJugador = Camera.main;
        if (camaraDelJugador == null)
        {
            PlayerController jugador = FindFirstObjectByType<PlayerController>();
            if (jugador != null) camaraDelJugador = jugador.GetComponentInChildren<Camera>();
        }
        if (camara == null)
        {
            var go = new GameObject("CamaraVitrina");
            go.transform.SetParent(transform, false);
            camara = go.AddComponent<Camera>();
        }
        camara.gameObject.SetActive(true);
        camara.fieldOfView = Campo;
        camara.nearClipPlane = 1f;
        camara.farClipPlane = Mathf.Max(4000f, camaraDelJugador != null ? camaraDelJugador.farClipPlane : 0f);
        camara.clearFlags = CameraClearFlags.Skybox;
        camara.depth = (camaraDelJugador != null ? camaraDelJugador.depth : 0f) + 10f;
        CalidadGrafica.AplicarACamara(camara);

        if (camaraDelJugador != null)
        {
            mascaraDelJugador = camaraDelJugador.cullingMask;
            camaraDelJugador.cullingMask = 0;
        }

        // --- Niebla -----------------------------------------------------
        niebla = RenderSettings.fog;
        nieblaInicio = RenderSettings.fogStartDistance;
        nieblaFin = RenderSettings.fogEndDistance;
        RenderSettings.fogStartDistance = radio * 1.4f;
        RenderSettings.fogEndDistance = radio * 6f;

        // --- HUD --------------------------------------------------------
        lienzosOcultos.Clear();
        HUDController hud = FindFirstObjectByType<HUDController>();
        if (hud != null)
        {
            foreach (Canvas c in hud.GetComponentsInChildren<Canvas>(true))
            {
                if (c.enabled)
                {
                    c.enabled = false;
                    lienzosOcultos.Add(c);
                }
            }
        }

        Mostrando = true;
        Colocar();
    }

    public void Ocultar()
    {
        if (!Mostrando) return;
        Mostrando = false;

        if (FloorManager.Instance != null) FloorManager.Instance.MostrarTodosLosPisos(false);
        if (camara != null) camara.gameObject.SetActive(false);
        if (camaraDelJugador != null) camaraDelJugador.cullingMask = mascaraDelJugador;

        RenderSettings.fog = niebla;
        RenderSettings.fogStartDistance = nieblaInicio;
        RenderSettings.fogEndDistance = nieblaFin;

        for (int i = 0; i < lienzosOcultos.Count; i++)
        {
            if (lienzosOcultos[i] != null) lienzosOcultos[i].enabled = true;
        }
        lienzosOcultos.Clear();
    }

    private void OnDestroy()
    {
        Ocultar();
    }

    private void LateUpdate()
    {
        if (!Mostrando) return;
        azimut += GradosPorSegundo * Time.unscaledDeltaTime;
        if (azimut >= 360f) azimut -= 360f;
        Colocar();
    }

    private void Colocar()
    {
        if (camara == null) return;

        // Un leve vaivén de altura para que no se sienta mecánico.
        float vaiven = Mathf.Sin(Time.unscaledTime * 0.18f) * altura * 0.06f;

        Quaternion giro = Quaternion.Euler(0f, azimut, 0f);
        Vector3 pos = centro + giro * (Vector3.back * radio) + Vector3.up * (altura * 0.62f + vaiven);
        camara.transform.position = pos;

        Vector3 mira = centro - Vector3.up * (altura * 0.05f);
        Quaternion hacia = Quaternion.LookRotation(mira - pos, Vector3.up);
        camara.transform.rotation = Quaternion.Euler(0f, -CorrimientoLateral, 0f) * hacia;
    }

    /// <summary>Caja de todo el edificio, para saber a qué distancia girar.</summary>
    private void Medir(FloorManager pisos)
    {
        if (medido) return;

        bool hay = false;
        Bounds caja = new Bounds();
        for (int i = 0; i < pisos.CantidadDePisos; i++)
        {
            Transform piso = pisos.Piso(i);
            if (piso == null) continue;
            foreach (Renderer r in piso.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!hay) { caja = r.bounds; hay = true; }
                else caja.Encapsulate(r.bounds);
            }
        }
        if (!hay)
        {
            caja = new Bounds(pisos.transform.position, new Vector3(40f, 60f, 60f));
        }

        centro = caja.center;
        altura = caja.size.y;
        float ancho = Mathf.Max(caja.size.x, caja.size.z);

        // Que quepa la altura entera con aire arriba y abajo, más la mitad del ancho.
        float porAltura = (altura * 0.56f) / Mathf.Tan(Campo * 0.5f * Mathf.Deg2Rad);
        float porAncho = (ancho * 0.75f) / Mathf.Tan(Campo * 0.5f * Mathf.Deg2Rad);
        radio = Mathf.Max(porAltura, porAncho) + ancho * 0.5f;

        azimut = 35f;
        medido = true;
    }
}
