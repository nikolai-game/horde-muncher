using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HordeMuncher.Editor
{
    public static class WaveProjectSetup
    {
        [MenuItem("Horde Muncher/Install Wave Configs")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }

            const string DIRECTORY = "Assets/Project/Configs";
            Directory.CreateDirectory(DIRECTORY);
            AssetDatabase.Refresh();
            const string ENEMY_PATH = DIRECTORY + "/BasicEnemy.asset";
            EnemyDefinition definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(ENEMY_PATH);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, ENEMY_PATH);
            }

            const string PREFAB_PATH = "Assets/Project/Prefabs/Enemy.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            try
            {
                EnemyAuthoring authoring = root.GetComponent<EnemyAuthoring>();
                if (authoring.Definition == null)
                {
                    authoring.Definition = definition;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            const string WAVES_PATH = DIRECTORY + "/ArenaWaves.asset";
            WaveSettings waves = AssetDatabase.LoadAssetAtPath<WaveSettings>(WAVES_PATH);
            if (waves == null)
            {
                waves = ScriptableObject.CreateInstance<WaveSettings>();
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
                foreach (WaveDefinition wave in waves.Waves)
                {
                    wave.EnemyPrefabs = new[]
                    {
                        prefab
                    };
                }

                AssetDatabase.CreateAsset(waves, WAVES_PATH);
            }

            const string SCENE_PATH = "Assets/Project/Scenes/ArenaEntities.unity";
            Scene scene = SceneManager.GetSceneByPath(SCENE_PATH);
            bool open = scene.IsValid() && scene.isLoaded;
            if (!open)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
            }

            try
            {
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                {
                    foreach (ArenaAuthoring authoring in sceneRoot.GetComponentsInChildren<ArenaAuthoring>(true))
                    {
                        if (authoring.Waves == null)
                        {
                            authoring.Waves = waves;
                        }

                        EditorUtility.SetDirty(authoring);
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!open)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.SaveAssets();
        }
    }
}
