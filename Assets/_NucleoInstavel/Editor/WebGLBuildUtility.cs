using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NucleoInstavel.Editor
{
    public static class WebGLBuildUtility
    {
        [MenuItem("Tools/Nucleo Instavel/Build WebGL")]
        public static void BuildWebGL()
        {
            ProjectSetup.ValidateProject();

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException(
                    "Instale WebGL Build Support para este Editor pelo Unity Hub."
                );

            if (
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL
                && !EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL,
                    BuildTarget.WebGL
                )
            )
                throw new InvalidOperationException(
                    "Não foi possível trocar para WebGL. Em batch use -buildTarget WebGL antes de executeMethod."
                );

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "PROJECT:NucleoInstavel";

            string output = GetBuildOutputPath();

            Directory.CreateDirectory(output);

            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.ScenePath },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                }
            );

            WriteBuildReport(report, output);

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Build WebGL: {report.summary.result}. Consulte local/webgl-build.md."
                );

            CreateUploadZip();
        }

        public static string GetBuildOutputPath() => Path.GetFullPath("Builds/WebGL");

        private static void WriteBuildReport(BuildReport report, string output)
        {
            Directory.CreateDirectory("local");

            using (var writer = new StreamWriter("local/webgl-build.md"))
            {
                writer.WriteLine(
                    $"# Build WebGL\n\nUTC: {DateTime.UtcNow:O}\n\nUnity: {Application.unityVersion}\n\nResultado: {report.summary.result}\n\nSaída: {output}\n\nErros: {report.summary.totalErrors}; avisos: {report.summary.totalWarnings}\n\nTempo: {report.summary.totalTime}\n\nLog do Editor: {Application.consoleLogPath}\n"
                );

                foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (
                        message.type == LogType.Warning
                        || message.type == LogType.Error
                        || message.type == LogType.Exception
                    )
                        writer.WriteLine($"- {step.name}: {message.type}: {message.content}");
            }
        }

        [MenuItem("Tools/Nucleo Instavel/Package Existing WebGL")]
        public static void CreateUploadZip()
        {
            string output = GetBuildOutputPath();

            if (
                !File.Exists(Path.Combine(output, "index.html"))
                || !Directory.Exists(Path.Combine(output, "Build"))
            )
                throw new InvalidOperationException(
                    "Build incompleta: index.html ou pasta Build ausente."
                );

            Directory.CreateDirectory("Releases");

            string zip = Path.GetFullPath("Releases/NucleoInstavel-WebGL.zip");
            string temporary = zip + ".tmp";

            if (File.Exists(temporary))
                File.Delete(temporary);

            ZipFile.CreateFromDirectory(
                output,
                temporary,
                System.IO.Compression.CompressionLevel.Optimal,
                false
            );

            using (var archive = ZipFile.OpenRead(temporary))
                if (archive.GetEntry("index.html") == null)
                    throw new InvalidOperationException("ZIP sem index.html na raiz.");

            if (File.Exists(zip))
                File.Delete(zip);

            File.Move(temporary, zip);
            Debug.Log("ZIP WebGL criado: " + zip);
        }
    }
}
