using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HordeMuncher.Editor
{
    public static class ProjectileProjectSetup
    {
        private static Material CreateMaterial(string name, Color color)
        {
            string path = "Assets/Project/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateModelPart(GameObject root, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        [MenuItem("Horde Muncher/Install Projectiles And Weapon Models")]
        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode first.");
            }

            EconomySettings settings = AssetDatabase.LoadAssetAtPath<EconomySettings>("Assets/Project/Configs/Economy.asset");
            if (settings == null)
            {
                throw new InvalidOperationException("Install economy first.");
            }

            Material metal = CreateMaterial("WeaponMetal", new Color(0.22f, 0.3f, 0.4f));
            Material grip = CreateMaterial("WeaponGrip", new Color(0.36f, 0.19f, 0.10f));
            Material accent = CreateMaterial("WeaponAccent", new Color(1, 0.65f, 0.08f));
            foreach (WeaponData weaponData in settings.Weapons)
            {
                if (weaponData.ModelPrefab != null)
                {
                    continue;
                }

                string path = "Assets/Project/Prefabs/Weapon" + weaponData.Weapon + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var root = new GameObject("Weapon" + weaponData.Weapon);
                    try
                    {
                        bool isPistol = weaponData.Weapon == WeaponKind.Pistol;
                        bool isShotgun = weaponData.Weapon == WeaponKind.Shotgun;
                        float muzzleOffsetZ = isPistol ? 0.55f : 0.8f;
                        CreateModelPart(root,
                            "Body",
                            new Vector3(0, 0, 0.08f),
                            new Vector3(isShotgun ? 0.3f : 0.22f, 0.22f, isPistol ? 0.65f : 0.8f),
                            metal);
                        CreateModelPart(root, "Grip", new Vector3(0, -0.2f, -0.12f), new Vector3(0.18f, 0.3f, 0.22f), grip);
                        CreateModelPart(root,
                            "Barrel",
                            new Vector3(0, 0, muzzleOffsetZ - 0.16f),
                            new Vector3(isShotgun ? 0.28f : 0.12f, 0.12f, 0.32f),
                            accent);
                        if (!isPistol)
                        {
                            CreateModelPart(root, "Stock", new Vector3(0, -0.03f, -0.52f), new Vector3(0.2f, 0.18f, 0.3f), grip);
                        }

                        if (!isPistol && !isShotgun)
                        {
                            CreateModelPart(root, "Magazine", new Vector3(0, -0.2f, 0.2f), new Vector3(0.16f, 0.35f, 0.22f), metal);
                        }

                        var socket = new GameObject("Muzzle");
                        socket.transform.SetParent(root.transform, false);
                        socket.transform.localPosition = new Vector3(0, 0, muzzleOffsetZ);
                        prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally
                    {
                        Object.DestroyImmediate(root);
                    }
                }

                weaponData.ModelPrefab = prefab;
                weaponData.MuzzleOffset = prefab.transform.Find("Muzzle").localPosition;
                weaponData.ProjectileSpeed = weaponData.Weapon == WeaponKind.Automatic ? 24 : weaponData.Weapon == WeaponKind.Shotgun ? 16 : 18;
                weaponData.ProjectileRadius = 0.12f;
                weaponData.ProjectileLifetime = 2;
                if (weaponData.Weapon == WeaponKind.Shotgun)
                {
                    weaponData.Description = "8 урона за дробину · 5 дробин · 0,8 с · 6 м";
                }
            }

            EditorUtility.SetDirty(settings);
            const string PROJECTILE_PATH = "Assets/Project/Prefabs/Projectile.prefab";
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_PATH);
            if (projectile == null)
            {
                GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                try
                {
                    root.name = "Projectile";
                    Object.DestroyImmediate(root.GetComponent<Collider>());
                    root.GetComponent<Renderer>().sharedMaterial = accent;
                    root.AddComponent<ProjectileAuthoring>();
                    GhostAuthoringComponent ghost = root.AddComponent<GhostAuthoringComponent>();
                    ghost.DefaultGhostMode = GhostMode.Interpolated;
                    ghost.SupportedGhostModes = GhostModeMask.Interpolated;
                    projectile = PrefabUtility.SaveAsPrefabAsset(root, PROJECTILE_PATH);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            const string SCENE_PATH = "Assets/Project/Scenes/ArenaEntities.unity";
            Scene scene = SceneManager.GetSceneByPath(SCENE_PATH);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
            }

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (ArenaAuthoring arena in root.GetComponentsInChildren<ArenaAuthoring>(true))
                    {
                        arena.ProjectilePrefab = projectile;
                        EditorUtility.SetDirty(arena);
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!loaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            ArenaMenu menu = Object.FindAnyObjectByType<ArenaMenu>();
            PlayerWeaponVisuals presentation = menu.GetComponent<PlayerWeaponVisuals>() ?? menu.gameObject.AddComponent<PlayerWeaponVisuals>();
            presentation.Settings = settings;
            EditorUtility.SetDirty(presentation);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
            AssetDatabase.SaveAssets();
            return "Installed projectile ghost, three weapon prefabs and presentation.";
        }
    }
}
