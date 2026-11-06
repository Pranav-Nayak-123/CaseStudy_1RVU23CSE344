using System.IO;
using UnityEditor;
using UnityEngine;

namespace CarDesigner.EditorTools
{
    /// <summary>
    /// Builds the studio scene the first time the project opens, and services command files dropped into
    /// Library/CarDesigner/ ("build", "preview") so the scene can be rebuilt while the editor is open.
    /// </summary>
    [InitializeOnLoad]
    static class StudioAutomation
    {
        const string RequestFolder = "Library/CarDesigner";
        const double PollSeconds = 1.0;

        static double nextPoll;

        static StudioAutomation()
        {
            EditorApplication.delayCall += BuildIfMissing;
            EditorApplication.update += Poll;
        }

        static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(StudioSceneBuilder.ScenePath) || !File.Exists(StudioSceneBuilder.ModelPath)) return;
            StudioSceneBuilder.Build();
            StudioSceneBuilder.CapturePreview("StudioPreview.png");
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + PollSeconds;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!Directory.Exists(RequestFolder)) return;

            foreach (string file in Directory.GetFiles(RequestFolder))
            {
                string command = Path.GetFileName(file);
                File.Delete(file);
                switch (command)
                {
                    case "build":
                        StudioSceneBuilder.Build();
                        StudioSceneBuilder.CapturePreview("StudioPreview.png");
                        break;
                    case "preview":
                        StudioSceneBuilder.CapturePreview("StudioPreview.png");
                        break;
                    default:
                        Debug.LogWarning($"CarDesigner: unknown automation command '{command}'.");
                        break;
                }
            }
        }
    }
}
