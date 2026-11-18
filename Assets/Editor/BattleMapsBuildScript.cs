#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BattleMaps.EditorTools
{
    public static class BattleMapsBuildScript
    {
        public static void BuildAndroid()
        {
            Debug.Log("[BattleMaps] Starting automated Android APK build...");

            // Set project branding & package settings
            PlayerSettings.companyName = "X Supreme";
            PlayerSettings.productName = "BattleMaps";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.xsupreme.battlemaps");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Ensure Main scene is generated
            string scenesDir = "Assets/Scenes";
            string scenePath = Path.Combine(scenesDir, "Main.unity");
            if (!File.Exists(scenePath))
            {
                BattleMapsProjectSetupEditor.CreateAndOpenMainScene();
            }

            string[] scenes = { scenePath };
            string outputPath = "build/Android/BattleMaps.apk";
            string outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BattleMaps] Build succeeded! Total size: {summary.totalSize} bytes at {outputPath}");
            }
            else
            {
                Debug.LogError($"[BattleMaps] Build failed with result: {summary.result}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
