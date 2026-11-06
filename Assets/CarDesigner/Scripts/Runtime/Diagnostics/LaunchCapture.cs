#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace CarDesigner.Diagnostics
{
    /// <summary>
    /// Development-build hook for unattended UI checks:
    ///   Player -atelierCapture out.png [-atelierScenario preset|doors|exploded|finish|switchcar]
    /// waits for the studio to settle, stages the scenario, writes a screenshot and quits.
    /// Stripped from release builds.
    /// </summary>
    public sealed class LaunchCapture : MonoBehaviour
    {
        string outputPath;
        string scenario;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string capture = Argument("-atelierCapture");
            if (capture == null) return;

            var host = new GameObject(nameof(LaunchCapture)).AddComponent<LaunchCapture>();
            host.outputPath = capture;
            host.scenario = Argument("-atelierScenario") ?? "idle";
        }

        static string Argument(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            Stage();
            yield return new WaitForSeconds(1.5f);

            ScreenCapture.CaptureScreenshot(outputPath);
            for (int i = 0; i < 10; i++) yield return null;
            Debug.Log($"LaunchCapture: '{scenario}' written to {outputPath}");
            Application.Quit();
        }

        void Stage()
        {
            var switcher = FindAnyObjectByType<CarSwitcher>();
            var selector = FindAnyObjectByType<PartSelector>();
            if (switcher == null || selector == null) return;
            if (scenario == "switchcar") switcher.SwitchTo((switcher.ActiveIndex + 1) % switcher.Count);
            CarRig rig = switcher.ActiveRig;
            if (rig == null) return;

            switch (scenario)
            {
                case "preset":
                    CarPart wheel = rig.Find("Tire_FL");
                    if (wheel == null) return;
                    selector.Select(wheel);
                    rig.SetEdit(wheel, DesignEdit.CaseStudy);
                    break;
                case "doors":
                    rig.DoorsOpen = true;
                    break;
                case "exploded":
                    rig.Exploded = true;
                    break;
                case "finish":
                    CarPaint paint = switcher.ActivePaint;
                    if (paint != null) paint.Apply(paint.Finishes.Count - 1);
                    break;
            }
        }
    }
}
#endif
