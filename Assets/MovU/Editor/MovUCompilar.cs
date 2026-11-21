using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ============================================================================
// MovUCompilar.cs — Compilación cruzada: Windows, Linux y macOS
// ============================================================================
// MovU > Compilar > Windows, Linux y Mac  deja en Builds/:
//   Builds/Windows/MovU.exe        (+ MovU_Data, UnityPlayer.dll...)
//   Builds/Linux/MovU.x86_64       (+ MovU_Data, UnityPlayer.so...)
//   Builds/Mac/MovU.app            (Intel y Apple Silicon en un solo .app)
// Siempre con la escena del edificio (Assets/MovU/Scenes/Edificio.unity), sin
// depender del orden de la lista de escenas del proyecto.
//
// Cada plataforma necesita su módulo en Unity Hub (Installs > 6000.3.21f1 >
// Add modules): "Linux Build Support (Mono)" y "Mac Build Support (Mono)". Si
// falta uno, se dice cuál y se sigue con las demás. Al terminar, el editor
// vuelve a la plataforma en la que estaba.
// ============================================================================

public static class MovUCompilar
{
    private const string Escena = "Assets/MovU/Scenes/Edificio.unity";
    private const string Carpeta = "Builds";

    private struct Destino
    {
        public string nombre, ruta, modulo;
        public BuildTarget target;
        public BuildTargetGroup grupo;
    }

    private static readonly Destino[] Destinos =
    {
        new Destino { nombre = "Windows", target = BuildTarget.StandaloneWindows64, grupo = BuildTargetGroup.Standalone,
                      ruta = Carpeta + "/Windows/MovU.exe", modulo = "Windows Build Support" },
        new Destino { nombre = "Linux", target = BuildTarget.StandaloneLinux64, grupo = BuildTargetGroup.Standalone,
                      ruta = Carpeta + "/Linux/MovU.x86_64", modulo = "Linux Build Support (Mono)" },
        new Destino { nombre = "Mac", target = BuildTarget.StandaloneOSX, grupo = BuildTargetGroup.Standalone,
                      ruta = Carpeta + "/Mac/MovU.app", modulo = "Mac Build Support (Mono)" },
    };

    [MenuItem("MovU/Compilar/Windows, Linux y Mac", priority = 1)]
    public static void Todas() => Compilar(Destinos);

    [MenuItem("MovU/Compilar/Solo Windows", priority = 20)]
    public static void Windows() => Compilar(new[] { Destinos[0] });

    [MenuItem("MovU/Compilar/Solo Linux", priority = 21)]
    public static void Linux() => Compilar(new[] { Destinos[1] });

    [MenuItem("MovU/Compilar/Solo Mac", priority = 22)]
    public static void Mac() => Compilar(new[] { Destinos[2] });

    [MenuItem("MovU/Compilar/Revisar módulos instalados", priority = 40)]
    public static void RevisarModulos()
    {
        var sb = new StringBuilder("[Compilar] Módulos de compilación:\n");
        foreach (Destino d in Destinos)
            sb.AppendLine($"  {d.nombre}: {(Instalado(d) ? "instalado" : "FALTA — Unity Hub > Installs > Add modules > " + d.modulo)}");
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Deja Builds/MovU_<Plataforma>.zip con la carpeta entera de la plataforma,
    /// para subirlo como archivo de un Release de GitHub (los compilados no van
    /// en el repositorio: Builds/ está en .gitignore).
    /// Ojo: un zip hecho en Windows no guarda el permiso de ejecución de Linux y
    /// Mac; en esos sistemas hay que darlo a mano (ver el README).
    /// </summary>
    private static string Comprimir(Destino d)
    {
        try
        {
            string carpeta = Path.GetDirectoryName(d.ruta);
            string zip = Path.Combine(Carpeta, $"MovU_{d.nombre}.zip");
            if (File.Exists(zip)) File.Delete(zip);
            // Sin las carpetas "..._DoNotShip" (símbolos de depuración de Burst/IL2CPP).
            using (var archivo = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            {
                string raiz = Path.GetFullPath(carpeta);
                foreach (string f in Directory.GetFiles(raiz, "*", SearchOption.AllDirectories))
                {
                    string relativo = f.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');
                    if (relativo.Contains("DoNotShip")) continue;
                    System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(archivo, f, relativo,
                        System.IO.Compression.CompressionLevel.Optimal);
                }
            }
            return $"comprimido en {zip} ({new FileInfo(zip).Length / (1024f * 1024f):F0} MB)";
        }
        catch (System.Exception e)
        {
            return "no se pudo comprimir: " + e.Message;
        }
    }

    private static bool Instalado(Destino d) => BuildPipeline.IsBuildTargetSupported(d.grupo, d.target);

    private static void Compilar(IEnumerable<Destino> destinos)
    {
        if (!File.Exists(Escena))
        {
            Debug.LogError("[Compilar] No encuentro " + Escena + ".");
            return;
        }
        BuildTarget antes = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup grupoAntes = BuildPipeline.GetBuildTargetGroup(antes);

        // macOS: un solo .app que corre en Intel y en Apple Silicon. Se pone por nombre
        // para que compile aunque el módulo de Mac todavía no esté instalado.
        EditorUserBuildSettings.SetPlatformSettings("OSXUniversal", "Architecture", "x64ARM64");

        var resumen = new StringBuilder("[Compilar] Resultado:\n");
        try
        {
            foreach (Destino d in destinos)
            {
                if (!Instalado(d))
                {
                    resumen.AppendLine($"  {d.nombre}: NO SE COMPILÓ — falta el módulo '{d.modulo}' (Unity Hub > Installs > 6000.3.21f1 > Add modules).");
                    continue;
                }
                var opciones = new BuildPlayerOptions
                {
                    scenes = new[] { Escena },
                    locationPathName = d.ruta,
                    target = d.target,
                    targetGroup = d.grupo,
                    options = BuildOptions.None,
                };
                BuildReport r = BuildPipeline.BuildPlayer(opciones);
                BuildSummary s = r.summary;
                resumen.AppendLine(s.result == BuildResult.Succeeded
                    ? $"  {d.nombre}: listo en {d.ruta} ({s.totalSize / (1024f * 1024f):F0} MB, {s.totalTime.TotalSeconds:F0} s)"
                    : $"  {d.nombre}: {s.result} ({s.totalErrors} errores). Revisa la consola.");
                if (s.result == BuildResult.Succeeded) resumen.AppendLine("      " + Comprimir(d));
            }
        }
        finally
        {
            if (EditorUserBuildSettings.activeBuildTarget != antes)
                EditorUserBuildSettings.SwitchActiveBuildTarget(grupoAntes, antes);
        }
        resumen.AppendLine("  Para repartir: se copia la carpeta entera de cada plataforma, no solo el ejecutable.");
        Debug.Log(resumen.ToString());
    }
}
