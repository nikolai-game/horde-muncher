using System.IO;
using Unity.NetCode;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HordeMuncher.Editor
{
    public static class ArenaProjectSetup
    {
        public const string SCENE_PATH = "Assets/Project/Scenes/Arena.unity";

        [MenuItem("Horde Muncher/Create Arena Scene")]
        public static void CreateArena()
        {
            if (File.Exists(SCENE_PATH))
            {
                throw new System.InvalidOperationException("Arena already exists. Open it instead of overwriting.");
            }

            Directory.CreateDirectory("Assets/Project/Scenes");
            Directory.CreateDirectory("Assets/Project/Prefabs");
            Directory.CreateDirectory("Assets/Project/Materials");
            AssetDatabase.Refresh();
            Material playerMaterial = CreateMaterial("Player", new Color(0.95f, 0.64f, 0.20f));
            Material floorMaterial = CreateMaterial("Floor", new Color(0.12f, 0.20f, 0.24f));
            Material wallMaterial = CreateMaterial("Walls", new Color(0.28f, 0.45f, 0.48f));
            Scene arenaScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(arenaScene);
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            Object.DestroyImmediate(player.GetComponent<Collider>());
            player.GetComponent<MeshRenderer>().sharedMaterial = playerMaterial;
            player.AddComponent<PlayerAuthoring>();
            GhostAuthoringComponent ghost = player.AddComponent<GhostAuthoringComponent>();
            ghost.HasOwner = true;
            ghost.SupportAutoCommandTarget = true;
            ghost.DefaultGhostMode = GhostMode.OwnerPredicted;
            ghost.SupportedGhostModes = GhostModeMask.All;
            GameObject playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, "Assets/Project/Prefabs/Player.prefab");
            Object.DestroyImmediate(player);
            CreateArenaBox("Arena Floor", new Vector3(0, -0.25f, 0), new Vector3(40, 0.5f, 40), floorMaterial);
            CreateArenaBox("North", new Vector3(0, 0.4f, 20), new Vector3(41, 0.8f, 1), wallMaterial);
            CreateArenaBox("South", new Vector3(0, 0.4f, -20), new Vector3(41, 0.8f, 1), wallMaterial);
            CreateArenaBox("East", new Vector3(20, 0.4f, 0), new Vector3(1, 0.8f, 40), wallMaterial);
            CreateArenaBox("West", new Vector3(-20, 0.4f, 0), new Vector3(1, 0.8f, 40), wallMaterial);
            for (int gridCoordinate = -15; gridCoordinate <= 15; gridCoordinate += 5)
            {
                CreateArenaBox("Grid X", new Vector3(gridCoordinate, 0.006f, 0), new Vector3(0.035f, 0.01f, 39), wallMaterial);
                CreateArenaBox("Grid Z", new Vector3(0, 0.006f, gridCoordinate), new Vector3(39, 0.01f, 0.035f), wallMaterial);
            }

            Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 18, -10);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true;
            camera.orthographicSize = 9;
            camera.gameObject.AddComponent<ArenaCameraFollow>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.065f, 0.09f);
            Light light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.6f);
            EditorSceneManager.SaveScene(arenaScene, SCENE_PATH);
            Scene entitiesScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(entitiesScene);
            ArenaAuthoring arenaAuthoring = new GameObject("Arena Configuration").AddComponent<ArenaAuthoring>();
            arenaAuthoring.PlayerPrefab = playerPrefab;
            string sharedPath = "Assets/Project/Scenes/ArenaEntities.unity";
            EditorSceneManager.SaveScene(entitiesScene, sharedPath);
            SceneManager.SetActiveScene(arenaScene);
            SubScene arenaSubScene = new GameObject("Arena Entities").AddComponent<SubScene>();
            arenaSubScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(sharedPath);
            arenaSubScene.AutoLoadScene = true;
            EditorSceneManager.CloseScene(entitiesScene, true);
            EditorSceneManager.SaveScene(arenaScene, SCENE_PATH);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SCENE_PATH, true)
            };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Horde Muncher/Play Start Menu")]
        public static void PlayStartMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(SCENE_PATH);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Horde Muncher/Install Start Menu In Arena")]
        public static void InstallMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new System.InvalidOperationException("Stop Play Mode before installing the menu.");
            }

            if (SceneManager.GetActiveScene().path != SCENE_PATH)
            {
                throw new System.InvalidOperationException("Open the Arena scene first.");
            }

            const string PANEL_PATH = "Assets/Project/UI/ArenaPanelSettings.asset";
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL_PATH);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 720);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 1;
                AssetDatabase.CreateAsset(panel, PANEL_PATH);
            }

            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Project/UI/ArenaTheme.tss");
            ArenaMenu existing = Object.FindFirstObjectByType<ArenaMenu>();
            GameObject menuObject = existing != null ? existing.gameObject : new GameObject("Arena Start Menu");
            UIDocument document = menuObject.GetComponent<UIDocument>() ?? menuObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/ArenaMenu.uxml");
            if (menuObject.GetComponent<ArenaSession>() == null)
            {
                menuObject.AddComponent<ArenaSession>();
            }

            if (existing == null)
            {
                menuObject.AddComponent<ArenaMenu>();
            }

            if (menuObject.GetComponent<ArenaApplication>() == null)
            {
                menuObject.AddComponent<ArenaApplication>();
            }

            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Horde Muncher/Install Enemy Crowd")]
        public static void InstallEnemies()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new System.InvalidOperationException("Stop Play Mode before installing enemies.");
            }

            const string PATH = "Assets/Project/Prefabs/Enemy.prefab";
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);
            if (enemyPrefab == null)
            {
                GameObject enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                try
                {
                    enemyObject.name = "Enemy";
                    enemyObject.transform.localScale = Vector3.one * 0.65f;
                    Object.DestroyImmediate(enemyObject.GetComponent<Collider>());
                    enemyObject.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial("Enemy", new Color(0.8f, 0.18f, 0.24f));
                    enemyObject.AddComponent<EnemyAuthoring>();
                    GhostAuthoringComponent ghost = enemyObject.AddComponent<GhostAuthoringComponent>();
                    ghost.DefaultGhostMode = GhostMode.Interpolated;
                    ghost.SupportedGhostModes = GhostModeMask.Interpolated;
                    enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemyObject, PATH);
                }
                finally
                {
                    Object.DestroyImmediate(enemyObject);
                }
            }

            const string SHARED_PATH = "Assets/Project/Scenes/ArenaEntities.unity";
            Scene entitiesScene = SceneManager.GetSceneByPath(SHARED_PATH);
            bool wasOpen = entitiesScene.IsValid() && entitiesScene.isLoaded;
            if (!wasOpen)
            {
                entitiesScene = EditorSceneManager.OpenScene(SHARED_PATH, OpenSceneMode.Additive);
            }

            try
            {
                foreach (GameObject sceneRoot in entitiesScene.GetRootGameObjects())
                {
                    foreach (ArenaAuthoring arenaAuthoring in sceneRoot.GetComponentsInChildren<ArenaAuthoring>(true))
                    {
                        arenaAuthoring.EnemyPrefab = enemyPrefab;
                        EditorUtility.SetDirty(arenaAuthoring);
                    }
                }

                EditorSceneManager.MarkSceneDirty(entitiesScene);
                EditorSceneManager.SaveScene(entitiesScene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(entitiesScene, true);
                }
            }
        }

        [MenuItem("Horde Muncher/Install Combat")]
        public static void InstallCombat()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new System.InvalidOperationException("Stop Play Mode before installing combat.");
            }

            if (SceneManager.GetActiveScene().path != SCENE_PATH)
            {
                throw new System.InvalidOperationException("Open the Arena scene first.");
            }

            InstallEnemies();
            const string PATH = "Assets/Project/Prefabs/Resource.prefab";
            GameObject resourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);
            if (resourcePrefab == null)
            {
                GameObject resourceObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                try
                {
                    resourceObject.name = "Resource";
                    resourceObject.transform.localScale = Vector3.one * 0.45f;
                    Object.DestroyImmediate(resourceObject.GetComponent<Collider>());
                    resourceObject.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial("Resource", new Color(0.2f, 0.95f, 0.45f));
                    resourceObject.AddComponent<ResourceAuthoring>();
                    GhostAuthoringComponent ghost = resourceObject.AddComponent<GhostAuthoringComponent>();
                    ghost.DefaultGhostMode = GhostMode.Interpolated;
                    ghost.SupportedGhostModes = GhostModeMask.Interpolated;
                    resourcePrefab = PrefabUtility.SaveAsPrefabAsset(resourceObject, PATH);
                }
                finally
                {
                    Object.DestroyImmediate(resourceObject);
                }
            }

            const string SHARED_PATH = "Assets/Project/Scenes/ArenaEntities.unity";
            Scene entitiesScene = SceneManager.GetSceneByPath(SHARED_PATH);
            bool wasOpen = entitiesScene.IsValid() && entitiesScene.isLoaded;
            if (!wasOpen)
            {
                entitiesScene = EditorSceneManager.OpenScene(SHARED_PATH, OpenSceneMode.Additive);
            }

            foreach (GameObject sceneRoot in entitiesScene.GetRootGameObjects())
            {
                foreach (ArenaAuthoring arenaAuthoring in sceneRoot.GetComponentsInChildren<ArenaAuthoring>(true))
                {
                    arenaAuthoring.ResourcePrefab = resourcePrefab;
                    EditorUtility.SetDirty(arenaAuthoring);
                }
            }

            EditorSceneManager.SaveScene(entitiesScene);
            if (!wasOpen)
            {
                EditorSceneManager.CloseScene(entitiesScene, true);
            }

            ArenaMenu menu = Object.FindFirstObjectByType<ArenaMenu>();
            PlayerStatusUiCompositionRoot playerStatus = menu.GetComponent<PlayerStatusUiCompositionRoot>();
            if (playerStatus == null)
            {
                playerStatus = menu.gameObject.AddComponent<PlayerStatusUiCompositionRoot>();
            }

            playerStatus.HudLayout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/CombatHud.uxml");
            playerStatus.PlayerLayout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/PlayerHud.uxml");
            EditorUtility.SetDirty(playerStatus);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Project/Materials/{name}.mat");
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = name,
                color = color,
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, $"Assets/Project/Materials/{name}.mat");
            return material;
        }

        private static void CreateArenaBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(box.GetComponent<Collider>());
        }
    }
}
