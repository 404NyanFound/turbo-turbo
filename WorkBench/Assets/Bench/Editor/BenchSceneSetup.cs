using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TurboTurbo.WorkBench
{
    public static class BenchSceneSetup
    {
        private const string ScenePath = "Assets/Bench/Exhaust.unity";

        [MenuItem("TurboTurbo/WorkBench Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("ExhaustBench");
            go.AddComponent<ExhaustBench>();

            GameAssetsImport.AddDe6ToScene();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"WorkBench: bench scene created at {ScenePath}");
        }
    }
}
