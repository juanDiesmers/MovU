using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================================
// MenuPrincipal.cs — Pantalla de carga, menú principal, opciones, controles y
// pausa (RF-13)
// ============================================================================
// Secuencia al dar Play / abrir el ejecutable:
//
//   carga  ->  menú principal (Comenzar · Controles · Opciones · Salir)
//          ->  panel de sesión del evaluador (TestSession)  ->  misiones
//
// Detrás del menú principal el edificio gira visto desde afuera (VitrinaMenu).
// El menú va en un panel a la izquierda con un degradado, y aparece desde negro
// al terminar la carga.
//
// Durante el juego, la tecla P abre la pausa (Continuar · Controles · Opciones ·
// Volver al menú · Salir). Pausar congela el reloj de la misión
// (Time.timeScale = 0).
//
// Opciones:
//   Gráficos  nivel Bajo / Medio / Alto (CalidadGrafica), resolución interna,
//             pantalla completa y contador de FPS.
//   Juego     sensibilidad del ratón y volumen (SaveSystem).
// Controles: la lista de teclas del juego.
//
// La "pantalla de carga" cubre el montaje del edificio (MovUBootstrap ya corrió
// cuando esto aparece): tapa la escena hasta que han pasado unos cuadros y un
// mínimo de tiempo. No es una carga asíncrona de escena.
//
// IMGUI, igual que TestSession: no añade objetos a la interfaz del juego.
// Se apaga con "menuPrincipal": false en el JSON.
// ============================================================================

public class MenuPrincipal : MonoBehaviour
{
    /// <summary>True mientras la carga o el menú principal están en pantalla (antes de la sesión).</summary>
    public static bool Activo { get; private set; }
    public static bool EnPausa { get; private set; }

    private static MenuPrincipal instancia;

    private enum Pantalla { Carga, Principal, Opciones, Controles, Pausa, PausaOpciones, PausaControles }
    private Pantalla pantalla;

    private const float TiempoMinimoDeCarga = 0.8f;
    private const int CuadrosMinimosDeCarga = 3;
    private const float DuracionAparicion = 1.4f;

    // Medidas del panel (en la resolución de referencia de 1080 de alto).
    private const float Margen = 120f;
    private const float AnchoContenido = 640f;

    private static readonly Color Acento = new Color(0.36f, 0.64f, 1f, 1f);

    private float inicioReal;
    private int cuadroInicial;
    private float inicioAparicion = -1f;

    private PlayerController jugador;
    private MouseLook mirada;
    private VitrinaMenu vitrina;

    private GUIStyle estiloTitulo, estiloTituloCarga, estiloSubtitulo, estiloLema, estiloSeccion, estiloEtiquetaInst,
                     estiloTexto, estiloDetalle, estiloPequeno, estiloBoton, estiloBotonPrincipal, estiloOpcion,
                     estiloOpcionElegida, estiloTecla, estiloToggle, estiloFps;
    private Texture2D fondoOpaco, fondoPausa, degradado, barra, barraRelleno, blanco;

    // Contador de FPS (opcional, en Opciones).
    private float fpsSuavizado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstaticos()
    {
        Activo = false;
        EnPausa = false;
        instancia = null;
    }

    public void Configurar()
    {
        instancia = this;
        Activo = true;
        EnPausa = false;
        pantalla = Pantalla.Carga;
        inicioReal = Time.realtimeSinceStartup;
        cuadroInicial = Time.frameCount;
        inicioAparicion = -1f;

        if (vitrina == null) vitrina = gameObject.AddComponent<VitrinaMenu>();

        AplicarOpciones();
    }

    /// <summary>Se salta el menú: para las pruebas automáticas y el menú de depuración.</summary>
    public static void Saltar()
    {
        Activo = false;
        if (instancia != null && instancia.vitrina != null) instancia.vitrina.Ocultar();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
        Activo = false;
        EnPausa = false;
        Time.timeScale = 1f;
    }

    private void BuscarJugador()
    {
        if (jugador == null) jugador = FindFirstObjectByType<PlayerController>();
        if (mirada == null) mirada = FindFirstObjectByType<MouseLook>();
    }

    private void AplicarOpciones()
    {
        AudioListener.volume = SaveSystem.Volumen;
        BuscarJugador();
        if (mirada != null) mirada.SetSensitivity(SaveSystem.Sensibilidad);
        CalidadGrafica.Aplicar();
    }

    // ------------------------------------------------------------------
    private void Update()
    {
        BuscarJugador();

        float dt = Time.unscaledDeltaTime;
        if (dt > 0f) fpsSuavizado = fpsSuavizado <= 0f ? 1f / dt : Mathf.Lerp(fpsSuavizado, 1f / dt, 0.05f);

        // El edificio girando, solo detrás del menú principal (no en la pausa).
        if (vitrina != null)
        {
            bool verVitrina = Activo && pantalla != Pantalla.Carga;
            if (verVitrina && !vitrina.Mostrando) vitrina.Mostrar();
            else if (!verVitrina && vitrina.Mostrando) vitrina.Ocultar();
        }

        Keyboard teclado = Keyboard.current;

        if (Activo)
        {
            if (pantalla == Pantalla.Carga &&
                Time.frameCount - cuadroInicial >= CuadrosMinimosDeCarga &&
                Time.realtimeSinceStartup - inicioReal >= TiempoMinimoDeCarga)
            {
                pantalla = Pantalla.Principal;
                inicioAparicion = Time.realtimeSinceStartup;
            }

            if (teclado != null && teclado.escapeKey.wasPressedThisFrame &&
                (pantalla == Pantalla.Opciones || pantalla == Pantalla.Controles))
            {
                VolverDeSubpantalla();
            }
            return;       // TestSession mantiene quieto al personaje mientras tanto
        }

        if (teclado == null) return;

        if (EnPausa)
        {
            // Esc lo usa PlayerController para soltar/atrapar el cursor: aquí se
            // mantiene suelto mientras dure la pausa.
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (teclado.escapeKey.wasPressedThisFrame &&
                (pantalla == Pantalla.PausaOpciones || pantalla == Pantalla.PausaControles))
            {
                VolverDeSubpantalla();
            }
            else if (teclado.pKey.wasPressedThisFrame)
            {
                Reanudar();
            }
            return;
        }

        if (TestSession.EsperandoInicio) return;

        // Solo se pausa si el jugador tiene el control y el cursor atrapado:
        // si hay otro panel abierto (ascensor, inventario) P no hace nada.
        if (teclado.pKey.wasPressedThisFrame &&
            jugador != null && jugador.InputEnabled && Cursor.lockState == CursorLockMode.Locked)
        {
            Pausar();
        }
    }

    private void VolverDeSubpantalla()
    {
        SaveSystem.GuardarOpciones();
        pantalla = EnPausa ? Pantalla.Pausa : Pantalla.Principal;
    }

    private void Pausar()
    {
        EnPausa = true;
        pantalla = Pantalla.Pausa;
        Time.timeScale = 0f;
        if (jugador != null) jugador.SetInputEnabled(false);
        if (mirada != null) mirada.SetLookEnabled(false);
    }

    private void Reanudar()
    {
        SaveSystem.GuardarOpciones();
        EnPausa = false;
        Time.timeScale = 1f;
        if (jugador != null)
        {
            jugador.SetInputEnabled(true);
            jugador.LockCursor();
        }
        if (mirada != null) mirada.SetLookEnabled(true);
    }

    private void Comenzar()
    {
        SaveSystem.GuardarOpciones();
        Activo = false;
        if (vitrina != null) vitrina.Ocultar();
    }

    private void Salir()
    {
        Time.timeScale = 1f;
        SaveSystem.GuardarOpciones();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------
    private void OnGUI()
    {
        PrepararEstilos();

        float k = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(k, k, 1f));
        float ancho = Screen.width / k;

        if (CalidadGrafica.MostrarFps)
        {
            GUI.Label(new Rect(ancho - 220f, 14f, 200f, 32f), $"{fpsSuavizado:F0} FPS", estiloFps);
        }

        if (!Activo && !EnPausa) return;

        var pantallaCompleta = new Rect(0f, 0f, ancho, 1080f);

        if (pantalla == Pantalla.Carga)
        {
            GUI.DrawTexture(pantallaCompleta, fondoOpaco);
            DibujarCarga(ancho);
            return;
        }

        Color antes = GUI.color;

        if (EnPausa)
        {
            GUI.DrawTexture(pantallaCompleta, fondoPausa);
        }
        else
        {
            // Aparece desde negro: primero se aclara el fondo y luego entra el menú.
            float t = inicioAparicion < 0f ? 1f : (Time.realtimeSinceStartup - inicioAparicion) / DuracionAparicion;
            float negro = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            if (negro > 0.001f)
            {
                GUI.color = new Color(1f, 1f, 1f, negro);
                GUI.DrawTexture(pantallaCompleta, fondoOpaco);
            }
            GUI.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.6f - 0.35f)));
        }

        // Panel de la izquierda: degradado de oscuro a transparente.
        GUI.DrawTexture(new Rect(0f, 0f, Mathf.Min(1150f, ancho), 1080f), degradado);

        switch (pantalla)
        {
            case Pantalla.Principal: DibujarPrincipal(ancho); break;
            case Pantalla.Opciones:
            case Pantalla.PausaOpciones: DibujarOpciones(ancho); break;
            case Pantalla.Controles:
            case Pantalla.PausaControles: DibujarControles(ancho); break;
            case Pantalla.Pausa: DibujarPausa(ancho); break;
        }

        GUI.color = antes;
    }

    private void DibujarCarga(float ancho)
    {
        GUI.Label(new Rect(0f, 400f, ancho, 110f), "MovU", estiloTituloCarga);
        GUI.Label(new Rect(0f, 515f, ancho, 40f), "Preparando el edificio…", estiloSubtituloCentrado);

        float t = Mathf.Clamp01((Time.realtimeSinceStartup - inicioReal) / TiempoMinimoDeCarga);
        var caja = new Rect(ancho * 0.5f - 260f, 585f, 520f, 4f);
        GUI.DrawTexture(caja, barra);
        GUI.DrawTexture(new Rect(caja.x, caja.y, caja.width * t, caja.height), barraRelleno);
    }

    private void Encabezado(string titulo, float y)
    {
        GUI.Label(new Rect(Margen, y, AnchoContenido, 90f), titulo, estiloTitulo);
        GUI.color = new Color(Acento.r, Acento.g, Acento.b, GUI.color.a);
        GUI.DrawTexture(new Rect(Margen + 4f, y + 96f, 70f, 4f), blanco);
        GUI.color = new Color(1f, 1f, 1f, GUI.color.a);
    }

    private void DibujarPrincipal(float ancho)
    {
        GUI.Label(new Rect(Margen + 4f, 205f, AnchoContenido, 30f),
                  "PONTIFICIA UNIVERSIDAD JAVERIANA  ·  INGENIERÍA", estiloEtiquetaInst);

        GUI.Label(new Rect(Margen - 4f, 230f, AnchoContenido, 160f), "MovU", estiloTituloGrande);

        GUI.color = new Color(Acento.r, Acento.g, Acento.b, GUI.color.a);
        GUI.DrawTexture(new Rect(Margen + 4f, 395f, 90f, 5f), blanco);
        GUI.color = new Color(1f, 1f, 1f, GUI.color.a);

        GUI.Label(new Rect(Margen, 418f, AnchoContenido, 44f), "Tu primer día en el edificio de Ingeniería", estiloSubtitulo);
        GUI.Label(new Rect(Margen, 462f, AnchoContenido, 34f), "Conoce tu edificio jugando.", estiloLema);

        float y = 560f;
        const float paso = 80f;
        if (GUI.Button(new Rect(Margen, y, 440f, 64f), "Comenzar", estiloBotonPrincipal)) Comenzar();
        if (GUI.Button(new Rect(Margen, y + paso, 440f, 64f), "Controles", estiloBoton)) pantalla = Pantalla.Controles;
        if (GUI.Button(new Rect(Margen, y + paso * 2f, 440f, 64f), "Opciones", estiloBoton)) pantalla = Pantalla.Opciones;
        if (GUI.Button(new Rect(Margen, y + paso * 3f, 440f, 64f), "Salir", estiloBoton)) Salir();

        GUI.Label(new Rect(Margen, 995f, AnchoContenido, 28f),
                  "Trabajo de grado  ·  Ingeniería de Sistemas  ·  2026", estiloPequeno);
        GUI.Label(new Rect(Margen, 1025f, AnchoContenido, 28f),
                  $"Versión {Application.version}  ·  Gráficos: {CalidadGrafica.Nombre(CalidadGrafica.Nivel)}", estiloPequeno);
    }

    private void DibujarPausa(float ancho)
    {
        Encabezado("Pausa", 220f);

        float y = 400f;
        const float paso = 80f;
        if (GUI.Button(new Rect(Margen, y, 460f, 64f), "Continuar   (P)", estiloBotonPrincipal)) Reanudar();
        if (GUI.Button(new Rect(Margen, y + paso, 460f, 64f), "Controles", estiloBoton)) pantalla = Pantalla.PausaControles;
        if (GUI.Button(new Rect(Margen, y + paso * 2f, 460f, 64f), "Opciones", estiloBoton)) pantalla = Pantalla.PausaOpciones;
        if (GUI.Button(new Rect(Margen, y + paso * 3f, 460f, 64f), "Volver al menú", estiloBoton))
        {
            if (GameManager.Instance != null) GameManager.Instance.AbandonarYVolverAlMenu();
            else Salir();
        }
        if (GUI.Button(new Rect(Margen, y + paso * 4f, 460f, 64f), "Salir", estiloBoton)) Salir();

        MissionManager mm = MissionManager.Instance;
        if (mm != null && mm.Estado == MissionManager.EstadoDeMisiones.EnCurso)
        {
            GUI.Label(new Rect(Margen, 820f, AnchoContenido, 60f),
                      "Si vuelves al menú, la misión en curso queda registrada como abandonada.",
                      estiloDetalle);
        }
    }

    // ------------------------------------------------------------------
    private void DibujarOpciones(float ancho)
    {
        Encabezado("Opciones", 110f);

        // --- Gráficos ---------------------------------------------------
        float y = 255f;
        GUI.Label(new Rect(Margen, y, AnchoContenido, 30f), "GRÁFICOS", estiloSeccion);

        NivelGrafico actual = CalidadGrafica.Nivel;
        float anchoOpcion = (AnchoContenido - 30f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var nivel = (NivelGrafico)i;
            var r = new Rect(Margen + i * (anchoOpcion + 15f), y + 40f, anchoOpcion, 56f);
            if (GUI.Button(r, CalidadGrafica.Nombre(nivel), nivel == actual ? estiloOpcionElegida : estiloOpcion) &&
                nivel != actual)
            {
                CalidadGrafica.ElegirNivel(nivel);
            }
        }

        GUI.Label(new Rect(Margen, y + 108f, AnchoContenido, 56f), CalidadGrafica.Descripcion(actual), estiloDetalle);
        GUI.Label(new Rect(Margen, y + 166f, AnchoContenido + 200f, 28f),
                  $"Sugerido para este equipo: {CalidadGrafica.Nombre(CalidadGrafica.Detectar())}  ·  {SystemInfo.graphicsDeviceName}",
                  estiloPequeno);

        float escala = CalidadGrafica.Escala;
        GUI.Label(new Rect(Margen, y + 210f, AnchoContenido, 34f), $"Resolución interna:  {Mathf.RoundToInt(escala * 100f)} %", estiloTexto);
        float nueva = GUI.HorizontalSlider(new Rect(Margen, y + 252f, AnchoContenido, 24f), escala,
                                           CalidadGrafica.EscalaMin, CalidadGrafica.EscalaMax);
        nueva = Mathf.Round(nueva * 20f) / 20f;      // de 5 en 5 %
        if (!Mathf.Approximately(nueva, escala)) CalidadGrafica.Escala = nueva;

        bool completa = GUI.Toggle(new Rect(Margen, y + 290f, 300f, 36f), Screen.fullScreen, "  Pantalla completa", estiloToggle);
        if (completa != Screen.fullScreen) Screen.fullScreen = completa;

        bool fps = GUI.Toggle(new Rect(Margen + 330f, y + 290f, 300f, 36f), CalidadGrafica.MostrarFps, "  Mostrar FPS", estiloToggle);
        if (fps != CalidadGrafica.MostrarFps) CalidadGrafica.MostrarFps = fps;

        // --- Juego ------------------------------------------------------
        y = 625f;
        GUI.Label(new Rect(Margen, y, AnchoContenido, 30f), "JUEGO", estiloSeccion);

        GUI.Label(new Rect(Margen, y + 40f, AnchoContenido, 34f), "Sensibilidad del ratón", estiloTexto);
        float sens = GUI.HorizontalSlider(new Rect(Margen, y + 82f, AnchoContenido, 24f), SaveSystem.Sensibilidad,
                                          SaveSystem.SensibilidadMin, SaveSystem.SensibilidadMax);
        if (!Mathf.Approximately(sens, SaveSystem.Sensibilidad))
        {
            SaveSystem.Sensibilidad = sens;
            if (mirada != null) mirada.SetSensitivity(sens);
        }

        GUI.Label(new Rect(Margen, y + 120f, AnchoContenido, 34f), $"Volumen:  {Mathf.RoundToInt(SaveSystem.Volumen * 100f)} %", estiloTexto);
        float vol = GUI.HorizontalSlider(new Rect(Margen, y + 162f, AnchoContenido, 24f), SaveSystem.Volumen, 0f, 1f);
        if (!Mathf.Approximately(vol, SaveSystem.Volumen))
        {
            SaveSystem.Volumen = vol;
            AudioListener.volume = vol;
        }

        // --- Botones ----------------------------------------------------
        float yb = 880f;
        if (GUI.Button(new Rect(Margen, yb, 310f, 60f), "Valores por defecto", estiloBoton))
        {
            SaveSystem.Sensibilidad = SaveSystem.SensibilidadPorDefecto;
            SaveSystem.Volumen = 1f;
            CalidadGrafica.ElegirNivel(CalidadGrafica.Detectar());
            AplicarOpciones();
        }
        if (GUI.Button(new Rect(Margen + 330f, yb, 310f, 60f), "Volver   (Esc)", estiloBotonPrincipal))
        {
            VolverDeSubpantalla();
        }
    }

    // ------------------------------------------------------------------
    private static readonly string[,] Teclas =
    {
        { "#", "MOVERSE" },
        { "W  A  S  D", "Caminar" },
        { "Ratón", "Mirar alrededor" },
        { "V", "Cámara en primera o tercera persona" },
        { "#", "INTERACTUAR" },
        { "E", "Ascensor, hablar con alguien o puerta de la escalera" },
        { "1 – 9", "Elegir el piso en el panel del ascensor (o clic)" },
        { "Esc", "Cerrar el panel del ascensor · soltar o atrapar el cursor" },
        { "#", "JUEGO" },
        { "G", "Cambiar el modo de la flecha guía" },
        { "I  /  Tab", "Inventario" },
        { "R", "Reiniciar la misión en curso" },
        { "P", "Pausa" },
        { "Enter", "Empezar, en el panel de sesión" },
    };

    private void DibujarControles(float ancho)
    {
        Encabezado("Controles", 110f);

        float y = 250f;
        for (int i = 0; i < Teclas.GetLength(0); i++)
        {
            string tecla = Teclas[i, 0], texto = Teclas[i, 1];
            if (tecla == "#")
            {
                y += 8f;
                GUI.Label(new Rect(Margen, y, AnchoContenido, 30f), texto, estiloSeccion);
                y += 38f;
                continue;
            }
            GUI.Label(new Rect(Margen, y, 150f, 40f), tecla, estiloTecla);
            GUI.Label(new Rect(Margen + 172f, y - 2f, AnchoContenido + 200f, 44f), texto, estiloTexto);
            y += 46f;
        }

        GUI.Label(new Rect(Margen, y + 14f, AnchoContenido + 60f, 50f),
                  "Sigue la flecha o pregúntale a la gente. La misión en curso aparece arriba en la pantalla.",
                  estiloDetalle);

        if (GUI.Button(new Rect(Margen, 975f, 310f, 60f), "Volver   (Esc)", estiloBotonPrincipal))
        {
            VolverDeSubpantalla();
        }
    }

    // ------------------------------------------------------------------
    private GUIStyle estiloTituloGrande, estiloSubtituloCentrado;

    private void PrepararEstilos()
    {
        if (estiloTitulo != null) return;

        blanco = Color1x1(Color.white);
        fondoOpaco = Color1x1(new Color(0.035f, 0.04f, 0.055f, 1f));
        fondoPausa = Color1x1(new Color(0.03f, 0.035f, 0.05f, 0.55f));
        barra = Color1x1(new Color(1f, 1f, 1f, 0.12f));
        barraRelleno = Color1x1(Acento);
        degradado = Degradado(new Color(0.035f, 0.045f, 0.065f, 1f), 0.93f);

        estiloTitulo = new GUIStyle(GUI.skin.label) { fontSize = 84, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        estiloTitulo.normal.textColor = Color.white;

        estiloTituloGrande = new GUIStyle(estiloTitulo) { fontSize = 140 };
        estiloTituloCarga = new GUIStyle(estiloTitulo) { fontSize = 96, alignment = TextAnchor.MiddleCenter };

        estiloSubtitulo = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleLeft };
        estiloSubtitulo.normal.textColor = new Color(1f, 1f, 1f, 0.88f);
        estiloSubtituloCentrado = new GUIStyle(estiloSubtitulo) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
        estiloSubtituloCentrado.normal.textColor = new Color(1f, 1f, 1f, 0.6f);

        estiloLema = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleLeft };
        estiloLema.normal.textColor = new Color(1f, 1f, 1f, 0.55f);

        estiloEtiquetaInst = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        estiloEtiquetaInst.normal.textColor = new Color(Acento.r, Acento.g, Acento.b, 0.95f);

        estiloSeccion = new GUIStyle(estiloEtiquetaInst) { fontSize = 19 };

        estiloTexto = new GUIStyle(GUI.skin.label) { fontSize = 25, alignment = TextAnchor.MiddleLeft, wordWrap = false };
        estiloTexto.normal.textColor = new Color(1f, 1f, 1f, 0.92f);

        estiloDetalle = new GUIStyle(GUI.skin.label) { fontSize = 21, alignment = TextAnchor.UpperLeft, wordWrap = true };
        estiloDetalle.normal.textColor = new Color(1f, 1f, 1f, 0.66f);

        estiloPequeno = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft, wordWrap = false };
        estiloPequeno.normal.textColor = new Color(1f, 1f, 1f, 0.45f);

        estiloBoton = BotonPlano(new Color(1f, 1f, 1f, 0.07f), new Color(1f, 1f, 1f, 0.16f), 28, TextAnchor.MiddleLeft);
        estiloBotonPrincipal = BotonPlano(new Color(Acento.r, Acento.g, Acento.b, 0.92f),
                                          new Color(Acento.r * 1.1f, Acento.g * 1.1f, 1f, 1f), 28, TextAnchor.MiddleLeft);
        estiloBotonPrincipal.fontStyle = FontStyle.Bold;

        estiloOpcion = BotonPlano(new Color(1f, 1f, 1f, 0.07f), new Color(1f, 1f, 1f, 0.16f), 25, TextAnchor.MiddleCenter);
        estiloOpcionElegida = BotonPlano(new Color(Acento.r, Acento.g, Acento.b, 0.92f),
                                         new Color(Acento.r, Acento.g, Acento.b, 1f), 25, TextAnchor.MiddleCenter);
        estiloOpcionElegida.fontStyle = FontStyle.Bold;

        estiloTecla = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        estiloTecla.normal.background = Color1x1(new Color(1f, 1f, 1f, 0.12f));
        estiloTecla.normal.textColor = Color.white;

        estiloToggle = new GUIStyle(GUI.skin.toggle) { fontSize = 23 };
        estiloToggle.normal.textColor = estiloToggle.hover.textColor =
            estiloToggle.onNormal.textColor = estiloToggle.onHover.textColor = new Color(1f, 1f, 1f, 0.92f);

        estiloFps = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        estiloFps.normal.textColor = new Color(0.55f, 1f, 0.6f, 0.95f);
    }

    private static GUIStyle BotonPlano(Color normal, Color encima, int tamano, TextAnchor alineacion)
    {
        var s = new GUIStyle(GUI.skin.button)
        {
            fontSize = tamano,
            alignment = alineacion,
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(alineacion == TextAnchor.MiddleLeft ? 30 : 8, 8, 4, 4),
        };
        Texture2D n = Color1x1(normal), h = Color1x1(encima);
        Texture2D a = Color1x1(new Color(normal.r * 0.8f, normal.g * 0.8f, normal.b * 0.8f, Mathf.Min(1f, normal.a + 0.1f)));
        s.normal.background = n;
        s.hover.background = h;
        s.focused.background = n;
        s.active.background = a;
        s.onNormal.background = n;
        s.onHover.background = h;
        s.onActive.background = a;
        Color texto = Color.white;
        s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = texto;
        return s;
    }

    /// <summary>Degradado horizontal: 'color' con alfa 'alfaIzq' a la izquierda, transparente a la derecha.</summary>
    private static Texture2D Degradado(Color color, float alfaIzq)
    {
        const int ancho = 256;
        var t = new Texture2D(ancho, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        for (int x = 0; x < ancho; x++)
        {
            float u = x / (ancho - 1f);
            // Plano hasta el 45 %, después cae suave.
            float a = u < 0.45f ? alfaIzq : alfaIzq * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.45f) / 0.55f));
            t.SetPixel(x, 0, new Color(color.r, color.g, color.b, a));
        }
        t.Apply();
        return t;
    }

    private static Texture2D Color1x1(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
