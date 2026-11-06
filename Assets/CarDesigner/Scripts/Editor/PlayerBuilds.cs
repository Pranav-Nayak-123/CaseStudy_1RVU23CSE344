using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarDesigner.EditorTools
{
    /// <summary>Player build entry points, usable from the menu or `Unity -batchmode -executeMethod`.</summary>
    public static class PlayerBuilds
    {
        const string Scene = "Assets/CarDesigner/Scenes/CarStudio.unity";

        [MenuItem("Car Designer/Build Android APK")]
        public static void Android()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Build(BuildTarget.Android, "Builds/Android/AtelierCarDesigner.apk", BuildOptions.None);
        }

        /// <summary>
        /// Static-hostable web build (GitHub Pages, itch.io, Netlify…). Gzip with decompression fallback
        /// loads even on hosts that don't send Content-Encoding headers.
        /// </summary>
        [MenuItem("Car Designer/Build Web (WebGL)")]
        public static void Web()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            PlayerSettings.WebGL.template = "PROJECT:Atelier";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            Build(BuildTarget.WebGL, "Builds/Web", BuildOptions.None);
        }

        [MenuItem("Car Designer/Build macOS Verification Player")]
        public static void MacVerification()
        {
            // Unattended captures must keep rendering while another app has focus.
            bool runInBackground = PlayerSettings.runInBackground;
            PlayerSettings.runInBackground = true;
            try
            {
                Build(BuildTarget.StandaloneOSX, "Builds/Verify/Atelier.app", BuildOptions.Development);
            }
            finally
            {
                PlayerSettings.runInBackground = runInBackground;
            }
        }

        [MenuItem("Car Designer/Build Windows Player")]
        public static void Windows()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/AtelierCarDesigner.exe", BuildOptions.None);
        }

        /// <summary>Development player for unattended LaunchCapture screenshots on Windows.</summary>
        public static void WindowsVerification()
        {
            bool runInBackground = PlayerSettings.runInBackground;
            PlayerSettings.runInBackground = true;
            try
            {
                Build(BuildTarget.StandaloneWindows64, "Builds/Verify/Atelier.exe", BuildOptions.Development);
            }
            finally
            {
                PlayerSettings.runInBackground = runInBackground;
            }
        }

        static void Build(BuildTarget target, string location, BuildOptions options)
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                target = target,
                locationPathName = location,
                options = options,
            });

            BuildSummary summary = report.summary;
            Debug.Log($"CarDesigner: {target} build {summary.result} — {summary.totalSize / (1024f * 1024f):0.0} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalTime.TotalSeconds:0}s → {location}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
