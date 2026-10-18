using System;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace HordeMuncher.Editor
{
    public static class CrowdVerification
    {
        [MenuItem("Horde Muncher/Verify Crowd Separation")]
        public static void RunMenu() => Run();

        public static string Run()
        {
            using var world = new World("Crowd verification");
            EntityManager entityManager = world.EntityManager;
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer), typeof(LocalTransform), typeof(CombatHealth));
            entityManager.SetComponentData(player, LocalTransform.FromPosition(0, 1, 0));
            entityManager.SetComponentData(player, new CombatHealth
            {
                Current = 100
            });
            SystemHandle system = world.GetOrCreateSystem<EnemyMovementSystem>();
            SimulationSystemGroup group = world.CreateSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(system);
            using EntityQuery query = entityManager.CreateEntityQuery(typeof(ArenaEnemy), typeof(LocalTransform));
            void SpawnEnemy(float positionX, float positionZ)
            {
                Entity enemy = entityManager.CreateEntity(typeof(ArenaEnemy), typeof(LocalTransform));
                entityManager.SetComponentData(enemy, LocalTransform.FromPosition(positionX, 0.65f, positionZ));
                entityManager.SetComponentData(enemy, new ArenaEnemy
                {
                    Speed = 2.5f,
                    StopDistance = 0.8f,
                    Radius = 0.325f,
                    SeparationGap = 0.2f,
                    SeparationSpeed = 12
                });
            }

            void AdvanceSimulation(int stepCount, float deltaTime)
            {
                for (int tickIndex = 0; tickIndex < stepCount; tickIndex++)
                {
                    world.SetTime(new TimeData(tickIndex * (double)deltaTime, deltaTime));
                    group.Update();
                }

                entityManager.CompleteAllTrackedJobs();
            }

            SpawnEnemy(0, 0);
            SpawnEnemy(0, 0);
            AdvanceSimulation(90, 1f / 60);
            using (NativeArray<LocalTransform> pair = query.ToComponentDataArray<LocalTransform>(Allocator.Temp))
            {
                if (math.distance(pair[0].Position, pair[1].Position) < 0.8f)
                {
                    throw new Exception("Coincident enemies failed to separate");
                }
            }

            entityManager.DestroyEntity(query);
            SpawnEnemy(-1, 0);
            SpawnEnemy(1, 0);
            using (NativeArray<Entity> pair = query.ToEntityArray(Allocator.Temp))
            {
                for (int enemyIndex = 0; enemyIndex < pair.Length; enemyIndex++)
                {
                    ArenaEnemy enemyData = entityManager.GetComponentData<ArenaEnemy>(pair[enemyIndex]);
                    enemyData.Speed = 0;
                    enemyData.Radius = enemyIndex == 0 ? 0.325f : 2;
                    entityManager.SetComponentData(pair[enemyIndex], enemyData);
                }
            }

            AdvanceSimulation(90, 1f / 60);
            using (NativeArray<LocalTransform> pair = query.ToComponentDataArray<LocalTransform>(Allocator.Temp))
            {
                if (math.distance(pair[0].Position, pair[1].Position) < 2.4f)
                {
                    throw new Exception("Mixed enemy sizes failed to separate");
                }
            }

            entityManager.DestroyEntity(query);
            for (int enemyIndex = 0; enemyIndex < 32; enemyIndex++)
            {
                SpawnEnemy(4, 0);
            }

            AdvanceSimulation(300, 1f / 60);
            using NativeArray<LocalTransform> positions = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            float nearest = float.MaxValue;
            float radius = 0;
            foreach (LocalTransform enemyTransform in positions)
            {
                if (!math.all(math.isfinite(enemyTransform.Position)) || math.any(math.abs(enemyTransform.Position.xz) > 18.5f))
                {
                    throw new Exception("Crowd position is invalid or outside arena");
                }

                radius = math.max(radius, math.length(enemyTransform.Position.xz));
            }

            for (int enemyIndex = 0; enemyIndex < positions.Length; enemyIndex++)
            {
                for (int neighbourIndex = enemyIndex + 1; neighbourIndex < positions.Length; neighbourIndex++)
                {
                    nearest = math.min(nearest, math.distance(positions[enemyIndex].Position, positions[neighbourIndex].Position));
                }
            }

            if (nearest < 0.25f || radius > 4)
            {
                throw new Exception($"Crowd collapsed or stopped chasing: nearest={nearest}, radius={radius}");
            }

            entityManager.SetComponentData(player, LocalTransform.FromPosition(18.5f, 1, 18.5f));
            AdvanceSimulation(180, 1f / 15);
            using NativeArray<LocalTransform> edge = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            foreach (LocalTransform enemyTransform in edge)
            {
                if (!math.all(math.isfinite(enemyTransform.Position)) || math.any(math.abs(enemyTransform.Position.xz) > 18.5f))
                {
                    throw new Exception("Batched tick boundary check failed");
                }
            }

            string result = $"Crowd checks passed: coincident pair, 32 enemies nearest={nearest:F3}m radius={radius:F3}m, "
                + "batched ticks and arena bounds.";
            Debug.Log(result);
            return result;
        }
    }
}
