using System;
using System.Reflection;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace HordeMuncher.Editor
{
    public static class PlayabilityVerification
    {
        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        [MenuItem("Horde Muncher/Verify Playability")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            VerifyLifecycle();
            VerifyWeapons();
            VerifyCamera();
            return "Passed: revival preserves builds, no revival on wipe/final wave, restart votes/replay/reset/second run, "
                + "weapon selling/replay/last weapon, piercing across ticks, shotgun knockback and bounds, "
                + "automatic retargeting, shared movement and camera framing.";
        }

        private static void VerifyLifecycle()
        {
            using var world = new World("Run lifecycle checks");
            EntityManager entityManager = world.EntityManager;
            Entity prefab = entityManager.CreateEntity(typeof(Prefab), typeof(CombatHealth), typeof(AutoWeapon));
            entityManager.SetComponentData(prefab, new CombatHealth
            {
                Current = 100,
                Maximum = 100
            });
            Entity runEntity = entityManager.CreateEntity(typeof(ArenaConfiguration),
                typeof(WaveRuntimeSettings),
                typeof(WaveProgress),
                typeof(EconomyConfig));
            var arenaConfiguration = new ArenaConfiguration
            {
                PlayerPrefab = prefab,
                MaxPlayers = 4
            };
            entityManager.SetComponentData(runEntity, arenaConfiguration);
            entityManager.SetComponentData(runEntity, new WaveRuntimeSettings
            {
                Seed = 17,
                HalfExtents = new float2(19)
            });
            entityManager.SetComponentData(runEntity, new EconomyConfig
            {
                StartingWeapon = 0,
                WeaponSlots = 6,
                FirstLevelXp = 5,
                WeaponSellFraction = 0.5f
            });
            entityManager.AddBuffer<CatalogEntry>(runEntity).Add(new CatalogEntry
            {
                Kind = GoodsKind.Weapon,
                Name = "Pistol",
                Price = 12,
                Weight = 1
            });
            entityManager.AddBuffer<WaveEnemy>(runEntity);
            entityManager.AddBuffer<WaveSpawnRateCurvePoint>(runEntity).Add(new WaveSpawnRateCurvePoint());
            entityManager.GetBuffer<WaveSpawnRateCurvePoint>(runEntity).Add(new WaveSpawnRateCurvePoint());
            entityManager.AddBuffer<WavePlan>(runEntity).Add(new WavePlan
            {
                Duration = 1,
                SpawnRatePointCount = 2
            });
            entityManager.GetBuffer<WavePlan>(runEntity).Add(new WavePlan
            {
                Duration = 1,
                SpawnRatePointCount = 2
            });
            Entity CreatePlayer(int networkId)
            {
                Entity playerEntity = entityManager.CreateEntity(typeof(ArenaPlayer),
                    typeof(GhostOwner),
                    typeof(CombatHealth),
                    typeof(AutoWeapon),
                    typeof(PlayerInput),
                    typeof(PlayerProgress),
                    typeof(PlayerBonuses),
                    typeof(CombatWallet),
                    typeof(WeaponShot),
                    typeof(LocalTransform),
                    typeof(WaveStatus));
                entityManager.SetComponentData(playerEntity, new GhostOwner
                {
                    NetworkId = networkId
                });
                entityManager.SetComponentData(playerEntity, new CombatHealth
                {
                    Current = 100,
                    Maximum = 100
                });
                entityManager.SetComponentData(playerEntity, LocalTransform.FromPosition(ArenaRunRules.CalculatePlayerSpawnPosition(networkId, 4)));
                entityManager.AddBuffer<EquippedWeapon>(playerEntity);
                entityManager.AddBuffer<OwnedItem>(playerEntity);
                entityManager.AddBuffer<ShopOffer>(playerEntity);
                entityManager.AddBuffer<UpgradeChoice>(playerEntity);
                return playerEntity;
            }

            Entity first = CreatePlayer(1);
            Entity second = CreatePlayer(2);
            Entity connection = entityManager.CreateEntity(typeof(NetworkId));
            entityManager.SetComponentData(connection, new NetworkId
            {
                Value = 1
            });
            Entity connection2 = entityManager.CreateEntity(typeof(NetworkId));
            entityManager.SetComponentData(connection2, new NetworkId
            {
                Value = 2
            });
            ShopSystem shop = world.CreateSystemManaged<ShopSystem>();
            WaveSpawnSystem waves = world.CreateSystemManaged<WaveSpawnSystem>();
            RunLifecycleSystem lifecycle = world.CreateSystemManaged<RunLifecycleSystem>();
            WaveCompletionSystem completion = world.CreateSystemManaged<WaveCompletionSystem>();
            void Tick()
            {
                world.SetTime(new TimeData(world.Time.ElapsedTime + 0.1, 0.1f));
                shop.Update();
                lifecycle.Update();
                waves.Update();
                completion.Update();
            }

            void QueueShopRequest(Entity player, Entity sourceConnection, ShopAction action, int commandSlot = 0, uint? revision = null)
            {
                Entity requestEntity = entityManager.CreateEntity(typeof(ShopRequest), typeof(ReceiveRpcCommandRequest));
                entityManager.SetComponentData(requestEntity, new ShopRequest
                {
                    Action = action,
                    Slot = commandSlot,
                    Revision = revision ?? entityManager.GetComponentData<PlayerProgress>(player).Revision
                });
                entityManager.SetComponentData(requestEntity, new ReceiveRpcCommandRequest
                {
                    SourceConnection = sourceConnection
                });
            }

            void FinishWaveCompletion()
            {
                Require(entityManager.GetComponentData<WaveProgress>(runEntity).Phase == WavePhase.WaveCleared,
                    "Wave skipped its completion phase");
                int completionTicks = 0;
                while (entityManager.GetComponentData<WaveProgress>(runEntity).Phase == WavePhase.WaveCleared)
                {
                    Tick();
                    Require(++completionTicks <= 30, "Wave completion phase did not finish");
                }
            }

            Tick();
            QueueShopRequest(first, connection, ShopAction.Restart);
            Tick();
            Require(!entityManager.GetComponentData<PlayerProgress>(first).RestartReady, "Restart allowed during combat");
            entityManager.SetComponentData(second, new CombatHealth
            {
                Current = 0,
                Maximum = 140
            });
            entityManager.SetComponentData(second, new PlayerBonuses
            {
                Damage = 0.3f
            });
            PlayerProgress playerProgress = entityManager.GetComponentData<PlayerProgress>(second);
            playerProgress.Level = 3;
            entityManager.SetComponentData(second, playerProgress);
            entityManager.GetBuffer<EquippedWeapon>(second).Add(new EquippedWeapon
            {
                CatalogId = 0
            });
            entityManager.SetComponentData(runEntity, new WaveProgress
            {
                Phase = WavePhase.Spawning,
                Elapsed = 0.95f,
                RandomState = 17
            });
            Tick();
            Require(entityManager.GetComponentData<CombatHealth>(second).Current == 0, "Player revived before wave completion");
            FinishWaveCompletion();
            Require(entityManager.GetComponentData<CombatHealth>(second).Current == 140, "Dead player not revived on shop entry");
            Require(entityManager.GetComponentData<PlayerBonuses>(second).Damage == 0.3f
                && entityManager.GetComponentData<PlayerProgress>(second).Level == 3
                && entityManager.GetBuffer<EquippedWeapon>(second).Length == 2, "Revival erased build");
            Require(entityManager.GetComponentData<CombatHealth>(first).Current == 100, "Revival changed survivor");
            Tick();
            int money = entityManager.GetComponentData<CombatWallet>(second).Resources;
            uint revision = entityManager.GetComponentData<PlayerProgress>(second).Revision;
            QueueShopRequest(second, connection2, ShopAction.Sell, 1, revision);
            QueueShopRequest(second, connection2, ShopAction.Sell, 1, revision);
            Tick();
            Require(entityManager.GetBuffer<EquippedWeapon>(second).Length == 1
                && entityManager.GetComponentData<CombatWallet>(second).Resources == money + 6, "Sale/replay refund incorrect");
            QueueShopRequest(second, connection2, ShopAction.Sell);
            Tick();
            Require(entityManager.GetBuffer<EquippedWeapon>(second).Length == 1
                && entityManager.GetComponentData<PlayerProgress>(second).Result == ShopResult.LastWeapon, "Last weapon sold");
            entityManager.SetComponentData(second, new CombatHealth
            {
                Current = 0,
                Maximum = 140
            });
            entityManager.SetComponentData(runEntity, new WaveProgress
            {
                Phase = WavePhase.Spawning,
                Index = 1,
                Elapsed = 0.95f,
                RandomState = 17
            });
            Tick();
            FinishWaveCompletion();
            Require(entityManager.GetComponentData<WaveProgress>(runEntity).Phase == WavePhase.Complete
                && entityManager.GetComponentData<CombatHealth>(second).Current == 0, "Final wave incorrectly revived");
            revision = entityManager.GetComponentData<PlayerProgress>(first).Revision;
            QueueShopRequest(first, connection, ShopAction.Restart, revision: revision);
            QueueShopRequest(first, connection, ShopAction.Restart, revision: revision);
            Tick();
            Require(entityManager.GetComponentData<PlayerProgress>(first).RestartReady
                && entityManager.GetComponentData<WaveProgress>(runEntity).Phase == WavePhase.Complete, "Replay vote or quorum failed");
            Entity oldEntity = first;
            QueueShopRequest(second, connection2, ShopAction.Restart);
            Tick();
            Tick();
            Require(entityManager.Exists(oldEntity) && entityManager.GetComponentData<GhostOwner>(second).NetworkId == 2,
                "Restart replaced party");
            Require(entityManager.GetComponentData<WaveProgress>(runEntity).Phase == WavePhase.Spawning
                && entityManager.GetComponentData<WaveProgress>(runEntity).Index == 0, "Restart did not start first wave");
            Require(entityManager.GetComponentData<CombatHealth>(second).Current == 100
                && entityManager.GetComponentData<PlayerBonuses>(second).Damage == 0
                && entityManager.GetComponentData<PlayerProgress>(second).Level == 1
                && entityManager.GetBuffer<EquippedWeapon>(second).Length == 1
                && entityManager.GetComponentData<CombatWallet>(second).Resources == 0, "Restart did not reset progression");
            entityManager.SetComponentData(first, new CombatHealth
            {
                Current = 0,
                Maximum = 100
            });
            entityManager.SetComponentData(second, new CombatHealth
            {
                Current = 0,
                Maximum = 100
            });
            Tick();
            Require(entityManager.GetComponentData<WaveProgress>(runEntity).Phase == WavePhase.Defeat
                && entityManager.GetComponentData<CombatHealth>(second).Current == 0, "Team wipe revived players");
            QueueShopRequest(first, connection, ShopAction.Restart);
            Tick();
            entityManager.DestroyEntity(second);
            Tick();
            Tick();
            Require(entityManager.GetComponentData<WaveProgress>(runEntity).Phase == WavePhase.Spawning,
                "Disconnected participant blocked restart");
        }

        private static void VerifyWeapons()
        {
            using var world = new World("Weapon role checks");
            EntityManager entityManager = world.EntityManager;
            Entity prefab = entityManager.CreateEntity(typeof(Prefab), typeof(ArenaProjectile), typeof(LocalTransform));
            Entity configurationEntity = entityManager.CreateEntity(typeof(ArenaConfiguration),
                typeof(WaveProgress),
                typeof(EconomyConfig),
                typeof(WaveRuntimeSettings));
            entityManager.SetComponentData(configurationEntity, new ArenaConfiguration
            {
                ProjectilePrefab = prefab
            });
            entityManager.SetComponentData(configurationEntity, new WaveProgress
            {
                Phase = WavePhase.Spawning
            });
            entityManager.SetComponentData(configurationEntity, new WaveRuntimeSettings
            {
                HalfExtents = new float2(19)
            });
            entityManager.AddBuffer<CatalogEntry>(configurationEntity).Add(new CatalogEntry
            {
                Weapon = WeaponKind.Automatic,
                RetargetOnKill = true,
                Damage = 7,
                Interval = 1,
                Range = 9,
                ProjectileSpeed = 10,
                ProjectileLifetime = 1
            });
            Entity CreateEnemy(float positionX)
            {
                Entity entity = entityManager.CreateEntity(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
                entityManager.SetComponentData(entity, new ArenaEnemy
                {
                    Radius = 0.3f
                });
                entityManager.SetComponentData(entity, new CombatHealth
                {
                    Current = 100,
                    Maximum = 100
                });
                entityManager.SetComponentData(entity, LocalTransform.FromPosition(positionX, 0, 0));
                return entity;
            }

            Entity firstEnemy = CreateEnemy(1);
            Entity secondEnemy = CreateEnemy(3);
            Entity thirdEnemy = CreateEnemy(5);
            Entity bullet = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(bullet, LocalTransform.Identity);
            entityManager.SetComponentData(bullet, new ArenaProjectile
            {
                Velocity = new float3(10, 0, 0),
                Damage = 15,
                Radius = 0.1f,
                Remaining = 2,
                ExtraPierceTargets = 1
            });
            ProjectileSystem flight = world.CreateSystemManaged<ProjectileSystem>();
            world.SetTime(new TimeData(0.1, 0.1f));
            flight.Update();
            Require(entityManager.Exists(bullet) && entityManager.GetComponentData<CombatHealth>(firstEnemy).Current == 85,
                "Piercing first hit failed");
            world.SetTime(new TimeData(0.11, 0.01f));
            flight.Update();
            Require(entityManager.GetComponentData<CombatHealth>(firstEnemy).Current == 85, "Piercing hit same body twice");
            world.SetTime(new TimeData(1, 0.8f));
            flight.Update();
            Require(!entityManager.Exists(bullet) && entityManager.GetComponentData<CombatHealth>(secondEnemy).Current == 85
                && entityManager.GetComponentData<CombatHealth>(thirdEnemy).Current == 100, "Piercing target limit failed");
            Entity pushed = CreateEnemy(18.65f);
            bullet = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(bullet, LocalTransform.FromPosition(18, 0, 0));
            entityManager.SetComponentData(bullet, new ArenaProjectile
            {
                Velocity = new float3(10, 0, 0),
                Damage = 8,
                Radius = 0.1f,
                Remaining = 1,
                KnockbackDistance = 0.25f
            });
            world.SetTime(new TimeData(1.1, 0.1f));
            flight.Update();
            Require(math.abs(entityManager.GetComponentData<LocalTransform>(pushed).Position.x - 18.7f) < 0.001f
                && entityManager.GetComponentData<CombatHealth>(pushed).Current == 92, "Knockback or arena bounds failed");
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer),
                typeof(CombatHealth),
                typeof(AutoWeapon),
                typeof(CombatWallet),
                typeof(WeaponShot),
                typeof(LocalTransform),
                typeof(PlayerBonuses));
            entityManager.SetComponentData(player, LocalTransform.Identity);
            entityManager.SetComponentData(player, new CombatHealth
            {
                Current = 100,
                Maximum = 100
            });
            entityManager.AddBuffer<EquippedWeapon>(player).Add(new EquippedWeapon
            {
                CatalogId = 0,
                LastTarget = firstEnemy,
                Cooldown = 1
            });
            entityManager.DestroyEntity(firstEnemy);
            WeaponFireSystem combat = world.CreateSystemManaged<WeaponFireSystem>();
            combat.Update();
            using EntityQuery bullets = entityManager.CreateEntityQuery(typeof(ArenaProjectile));
            Require(bullets.CalculateEntityCount() == 1 && entityManager.GetBuffer<EquippedWeapon>(player)[0].LastTarget == secondEnemy,
                "Automatic did not immediately switch dead target");
        }

        private static void VerifyCamera()
        {
            using (var world = new World("Shared movement job checks"))
            {
                EntityManager entityManager = world.EntityManager;
                Entity CreatePlayer(float positionX, float input, int group)
                {
                    Entity entity = entityManager.CreateEntity(typeof(ArenaPlayer),
                        typeof(Simulate),
                        typeof(PlayerInput),
                        typeof(PlayerMovement),
                        typeof(LocalTransform),
                        typeof(CombatHealth),
                        typeof(PlayerBonuses),
                        typeof(WaveStatus));
                    entityManager.SetComponentData(entity, new ArenaPlayer
                    {
                        SharedViewGroup = group,
                        SharedViewMaxDistance = 22
                    });
                    entityManager.SetComponentData(entity, new PlayerInput
                    {
                        Move = new float2(input, 0),
                        ViewDistance = 22
                    });
                    entityManager.SetComponentData(entity, new PlayerMovement
                    {
                        Speed = 7,
                        HalfExtent = 18.5f
                    });
                    entityManager.SetComponentData(entity, LocalTransform.FromPosition(positionX, 1, 0));
                    entityManager.SetComponentData(entity, new CombatHealth
                    {
                        Current = 100,
                        Maximum = 100
                    });
                    entityManager.SetComponentData(entity, new WaveStatus
                    {
                        Phase = WavePhase.Spawning
                    });
                    return entity;
                }

                Entity first = CreatePlayer(-10, -1, 1);
                Entity second = CreatePlayer(10, 1, 1);
                SimulationSystemGroup sharedViewGroup = world.CreateSystemManaged<SimulationSystemGroup>();
                sharedViewGroup.AddSystemToUpdateList(world.GetOrCreateSystem<PlayerMovementSystem>());
                void Step()
                {
                    world.SetTime(new TimeData(world.Time.ElapsedTime + 1, 1));
                    sharedViewGroup.Update();
                    entityManager.CompleteAllTrackedJobs();
                }

                Step();
                Require(math.abs(math.distance(entityManager.GetComponentData<LocalTransform>(first).Position,
                    entityManager.GetComponentData<LocalTransform>(second).Position)
                    - 34)
                    < 0.001f, "Shared camera constrained player separation");
                entityManager.SetComponentData(second, new CombatHealth());
                Step();
                Require(entityManager.GetComponentData<LocalTransform>(first).Position.x < -17, "Dead teammate constrained movement");
                entityManager.SetComponentData(first, new ArenaPlayer());
                entityManager.SetComponentData(second, new ArenaPlayer());
                entityManager.SetComponentData(first, LocalTransform.FromPosition(-10, 1, 0));
                entityManager.SetComponentData(second, LocalTransform.FromPosition(10, 1, 0));
                entityManager.SetComponentData(second, new CombatHealth
                {
                    Current = 100
                });
                Step();
                Require(math.distance(entityManager.GetComponentData<LocalTransform>(first).Position,
                    entityManager.GetComponentData<LocalTransform>(second).Position)
                    > 30, "Independent cameras were constrained");
            }

            foreach (float aspect in new[]
            {
                0.75f,
                1f,
                16f / 9
            }

                )
            {
                float size = ArenaCameraFollow.CalculateRequiredViewSize(Vector3.right,
                    Vector3.up,
                    new Vector3(18.5f, 18.5f, 0),
                    Vector3.zero,
                    aspect,
                    3);
                Require(size >= 21.5f && size * aspect >= 21.499f, "Shared view cannot fit players across the arena");
            }

            using var cameraWorld = new World("Shared camera framing checks", WorldFlags.GameClient);
            EntityManager cameraEntityManager = cameraWorld.EntityManager;
            Entity firstCameraPlayer = cameraEntityManager.CreateEntity(typeof(ArenaPlayer), typeof(GhostOwnerIsLocal),
                typeof(LocalTransform), typeof(CombatHealth));
            Entity secondCameraPlayer = cameraEntityManager.CreateEntity(typeof(ArenaPlayer), typeof(GhostOwnerIsLocal),
                typeof(LocalTransform), typeof(CombatHealth));
            cameraEntityManager.SetComponentData(firstCameraPlayer, new CombatHealth { Current = 100 });
            cameraEntityManager.SetComponentData(secondCameraPlayer, new CombatHealth { Current = 100 });
            var cameraObject = new GameObject("Shared camera framing test");
            try
            {
                ArenaCameraFollow follow = cameraObject.AddComponent<ArenaCameraFollow>();
                Camera camera = cameraObject.GetComponent<Camera>();
                const BindingFlags LIFECYCLE_METHOD_FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ArenaCameraFollow).GetMethod("Awake", LIFECYCLE_METHOD_FLAGS).Invoke(follow, null);
                MethodInfo updateCamera = typeof(ArenaCameraFollow).GetMethod("LateUpdate", LIFECYCLE_METHOD_FLAGS);
                cameraEntityManager.SetComponentData(firstCameraPlayer, LocalTransform.FromPosition(-1, 1, 0));
                cameraEntityManager.SetComponentData(secondCameraPlayer, LocalTransform.FromPosition(1, 1, 0));
                updateCamera.Invoke(follow, null);
                Require(math.abs(camera.orthographicSize - follow.ViewSize) < 0.001f, "Nearby players changed normal camera scale");
                foreach (float aspect in new[] { 0.75f, 1f, 16f / 9 })
                {
                    camera.aspect = aspect;
                    var firstPosition = new Vector3(-18.5f, 1, -18.5f);
                    var secondPosition = new Vector3(16.5f, 1, 18.5f);
                    cameraEntityManager.SetComponentData(firstCameraPlayer, LocalTransform.FromPosition(firstPosition));
                    cameraEntityManager.SetComponentData(secondCameraPlayer, LocalTransform.FromPosition(secondPosition));
                    updateCamera.Invoke(follow, null);
                    Require(Vector3.Distance(camera.transform.position, (firstPosition + secondPosition) * 0.5f + follow.Offset) < 0.001f,
                        "Separated players were not centered");
                    foreach (Vector3 playerPosition in new[] { firstPosition, secondPosition })
                    {
                        Vector3 viewportPosition = camera.WorldToViewportPoint(playerPosition);
                        Require(viewportPosition.x > 0 && viewportPosition.x < 1 && viewportPosition.y > 0 && viewportPosition.y < 1
                            && viewportPosition.z > 0, "Separated player left the camera frame");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
