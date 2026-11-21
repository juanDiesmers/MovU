using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// ============================================================================
// MovUMenuVistas.cs — Saltar a una pantalla del menú en Play (para revisarla o
// capturarla sin tener que hacer clic)
// ============================================================================
// MovU > Depuracion en Play > Menú: Principal / Opciones / Controles / Pausa
// Usa reflexión sobre el campo privado 'pantalla' de MenuPrincipal: no añade
// nada al código del juego.
// ============================================================================

public static class MovUMenuVistas
{
    private const string Ruta = "MovU/Depuracion en Play/Menú: ";

    [MenuItem(Ruta + "Principal", priority = 300)] private static void Principal() => Ir("Principal");
    [MenuItem(Ruta + "Opciones", priority = 301)] private static void Opciones() => Ir("Opciones");
    [MenuItem(Ruta + "Controles", priority = 302)] private static void Controles() => Ir("Controles");

    [MenuItem(Ruta + "Comenzar", priority = 303)]
    private static void Comenzar()
    {
        MenuPrincipal menu = UnityEngine.Object.FindFirstObjectByType<MenuPrincipal>();
        MethodInfo m = typeof(MenuPrincipal).GetMethod("Comenzar", BindingFlags.NonPublic | BindingFlags.Instance);
        if (menu != null && m != null) m.Invoke(menu, null);
    }

    [MenuItem("MovU/Depuracion en Play/Gráficos: Bajo", priority = 320)] private static void Bajo() => CalidadGrafica.ElegirNivel(NivelGrafico.Bajo);
    [MenuItem("MovU/Depuracion en Play/Gráficos: Medio", priority = 321)] private static void Medio() => CalidadGrafica.ElegirNivel(NivelGrafico.Medio);
    [MenuItem("MovU/Depuracion en Play/Gráficos: Alto", priority = 322)] private static void Alto() => CalidadGrafica.ElegirNivel(NivelGrafico.Alto);

    // En modo edición cambiarían el asset de URP del proyecto: solo en Play.
    [MenuItem("MovU/Depuracion en Play/Gráficos: Bajo", true)]
    [MenuItem("MovU/Depuracion en Play/Gráficos: Medio", true)]
    [MenuItem("MovU/Depuracion en Play/Gráficos: Alto", true)]
    private static bool SoloEnPlay() => Application.isPlaying;

    [MenuItem(Ruta + "Principal", true)]
    [MenuItem(Ruta + "Opciones", true)]
    [MenuItem(Ruta + "Controles", true)]
    [MenuItem(Ruta + "Comenzar", true)]
    private static bool EnPlay() => Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<MenuPrincipal>() != null;

    private static void Ir(string nombre)
    {
        MenuPrincipal menu = UnityEngine.Object.FindFirstObjectByType<MenuPrincipal>();
        if (menu == null) return;

        FieldInfo campo = typeof(MenuPrincipal).GetField("pantalla", BindingFlags.NonPublic | BindingFlags.Instance);
        if (campo == null)
        {
            Debug.LogWarning("[Menú] MenuPrincipal ya no tiene el campo 'pantalla'.");
            return;
        }
        if (!MenuPrincipal.Activo)
        {
            Debug.LogWarning("[Menú] El menú principal ya se cerró (se pulsó Comenzar).");
            return;
        }
        campo.SetValue(menu, Enum.Parse(campo.FieldType, nombre));
    }
}
