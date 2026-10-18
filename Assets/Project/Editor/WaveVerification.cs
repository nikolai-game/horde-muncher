using System;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace HordeMuncher.Editor
{
    public static class WaveVerification
    {
        [MenuItem("Horde Muncher/Verify Wave Rules")]
        public static void RunMenu() => Run();

        public static string Run()
        {
            using var world = new World("Wave checks");
            EntityManager entityManager = world.EntityManager;
            Entity configurationEntity = entityManager.CreateEntity(typeof(WaveRuntimeSettings), typeof(WaveProgress));
            var settings = new WaveRuntimeSettings
            {
                HalfExtents = new float2(19),
                SafeDistance = 6,
                MaxAlive = 500,
                MaxSpawnsPerTick = 16,
                PositionAttempts = 24,
                Seed = 17
            };
            entityManager.SetComponentData(configurationEntity, settings);
            entityManager.AddBuffer<WavePlan>(configurationEntity);
            entityManager.AddBuffer<WaveSpawnRateCurvePoint>(configurationEntity);
            entityManager.AddBuffer<WaveEnemy>(configurationEntity);
            Entity prefab = entityManager.CreateEntity(typeof(Prefab), typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
            entityManager.SetComponentData(prefab, new CombatHealth { Current = 30, Maximum = 30 });
            entityManager.SetComponentData(prefab, new ArenaEnemy
            {
                Radius = 0.325f,
                SpawnHeight = 0.65f
            });
            entityManager.SetComponentData(prefab, LocalTransform.Identity);
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer),
                typeof(CombatHealth),
                typeof(LocalTransform),
                typeof(WaveStatus),
                typeof(PlayerProgress));
            entityManager.SetComponentData(player, new CombatHealth
            {
                Current = 100,
                Maximum = 100
            });
            entityManager.SetComponentData(player, LocalTransform.Identity);
            entityManager.GetBuffer<WaveEnemy>(configurationEntity).Add(new WaveEnemy
            {
                Prefab = prefab
            });
            DynamicBuffer<WaveSpawnRateCurvePoint> rates = entityManager.GetBuffer<WaveSpawnRateCurvePoint>(configurationEntity);
            rates.Add(new WaveSpawnRateCurvePoint
            {
                Rate = 0.5f
            });
            rates.Add(new WaveSpawnRateCurvePoint
            {
                Rate = 3
            });
            var plan = new WavePlan
            {
                Duration = 20,
                SpawnRatePointCount = 2,
                EnemyCount = 1
            };
            float split = 0;
            for (int tickIndex = 0; tickIndex < 200; tickIndex++)
            {
                split += WaveMath.CalculateSpawnAmount(rates, plan, tickIndex * 0.1f, (tickIndex + 1) * 0.1f);
            }

            if (math.abs(split - 35) > 0.001f || math.abs(WaveMath.CalculateSpawnAmount(rates, plan, 0, 20) - split) > 0.001f)
            {
                throw new Exception("Curve integration depends on step size");
            }

            foreach (float duration in new[]
            {
                20f,
                30f,
                60f
            }

                )
            {
                plan.Duration = duration;
                entityManager.GetBuffer<WavePlan>(configurationEntity).Add(plan);
            }

            RunLifecycleSystem lifecycle = world.CreateSystemManaged<RunLifecycleSystem>();
            WaveSpawnSystem system = world.CreateSystemManaged<WaveSpawnSystem>();
            WaveCompletionSystem completion = world.CreateSystemManaged<WaveCompletionSystem>();
            using EntityQuery enemies = entityManager.CreateEntityQuery(typeof(ArenaEnemy), typeof(LocalTransform));
            double time = 0;
            void Step(float deltaTime = 0.1f)
            {
                time += deltaTime;
                world.SetTime(new TimeData(time, deltaTime));
                lifecycle.Update();
                system.Update();
                completion.Update();
            }

            for (int waveIndex = 0; waveIndex < 3; waveIndex++)
            {
                int steps = 0;
                do
                {
                    Step();
                    if (++steps > 610)
                    {
                        throw new Exception("Wave did not finish on time");
                    }
                }
                while (entityManager.GetComponentData<WaveProgress>(configurationEntity).Index == waveIndex
                    && entityManager.GetComponentData<WaveProgress>(configurationEntity).Phase == WavePhase.Spawning);
                WaveProgress progress = entityManager.GetComponentData<WaveProgress>(configurationEntity);
                float expected = new[]
                {
                    20f,
                    30f,
                    60f
                }[waveIndex];
                if (math.abs(steps * 0.1f - expected) > 0.21f)
                {
                    throw new Exception("Wave duration mismatch");
                }

                if (enemies.CalculateEntityCount() != 0)
                {
                    throw new Exception("Wave left enemies on the map");
                }

                if (progress.Phase != WavePhase.WaveCleared)
                {
                    throw new Exception("Wave skipped its completion phase");
                }

                int completionSteps = 0;
                while (entityManager.GetComponentData<WaveProgress>(configurationEntity).Phase == WavePhase.WaveCleared)
                {
                    Step();
                    if (++completionSteps > 30)
                    {
                        throw new Exception("Wave completion phase did not finish");
                    }
                }

                if (waveIndex < 2)
                {
                    entityManager.SetComponentData(player, new PlayerProgress { ShopWave = waveIndex });
                    Step();
                    progress = entityManager.GetComponentData<WaveProgress>(configurationEntity);
                    if (progress.Index != waveIndex || progress.Phase != WavePhase.Shop)
                    {
                        throw new Exception("Missing post-wave shop");
                    }

                    Step();
                    if (entityManager.GetComponentData<WaveProgress>(configurationEntity).Phase != WavePhase.Shop)
                    {
                        throw new Exception("Shop skipped unready player");
                    }

                    entityManager.SetComponentData(player, new PlayerProgress
                    {
                        Ready = true,
                        ShopWave = waveIndex
                    });
                    Step();
                    if (entityManager.GetComponentData<WaveProgress>(configurationEntity).Index != waveIndex + 1)
                    {
                        throw new Exception("Ready did not advance wave");
                    }

                    entityManager.SetComponentData(player, new PlayerProgress());
                }
            }

            if (entityManager.GetComponentData<WaveProgress>(configurationEntity).Phase != WavePhase.Complete)
            {
                throw new Exception("Final wave never completed");
            }

            Step();
            entityManager.DestroyEntity(enemies);
            entityManager.SetComponentData(configurationEntity, new WaveProgress());
            settings.MaxAlive = 2;
            entityManager.SetComponentData(configurationEntity, settings);
            for (int tickIndex = 0; tickIndex < 100; tickIndex++)
            {
                Step();
            }

            if (enemies.CalculateEntityCount() != 2)
            {
                throw new Exception("Population cap failed");
            }

            entityManager.DestroyEntity(enemies);
            settings.SafeDistance = 100;
            entityManager.SetComponentData(configurationEntity, settings);
            entityManager.SetComponentData(configurationEntity, new WaveProgress());
            for (int tickIndex = 0; tickIndex < 100; tickIndex++)
            {
                Step();
            }

            if (enemies.CalculateEntityCount() != 0)
            {
                throw new Exception("Impossible spawn distance ignored");
            }

            entityManager.SetComponentData(player, new CombatHealth
            {
                Current = 0
            });
            Step();
            if (entityManager.GetComponentData<WaveStatus>(player).Phase != WavePhase.Defeat)
            {
                throw new Exception("Defeat did not stop waves");
            }

            return "Passed: curve integral, 20/30/60 seconds, random safe positions, post-wave shops, ready gate "
                + "and field cleanup, completion, population cap, impossible placement and defeat.";
        }
    }
}
