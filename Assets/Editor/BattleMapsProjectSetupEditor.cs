#if UNITY_EDITOR
using System.IO;
using BattleMaps.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleMaps.EditorTools
{
    /// <summary>
    /// One-click Unity Editor setup helper: creates Assets/Scenes/Main.unity wired with BattleMapsRuntimeBootstrapper.
    /// </summary>
    public static class BattleMapsProjectSetupEditor
    {
        [MenuItem("BattleMaps/Create & Open Main Scene", priority = 1)]
        public static void CreateAndOpenMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var root = new GameObject("BattleMaps_RuntimeRoot");
            root.AddComponent<BattleMapsRuntimeBootstrapper>();

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.11f, 0.14f, 0.22f);
                cam.transform.position = new Vector3(0f, 24f, -12f);
                cam.transform.LookAt(Vector3.zero);
            }

            string scenesDir = "Assets/Scenes";
            if (!Directory.Exists(scenesDir))
                Directory.CreateDirectory(scenesDir);

            string scenePath = Path.Combine(scenesDir, "Main.unity");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            Debug.Log("[BattleMaps] Created and opened Assets/Scenes/Main.unity. Press Play!");
        }
    }
}
#endif
