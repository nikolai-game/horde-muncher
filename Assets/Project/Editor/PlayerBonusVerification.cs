using System;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace HordeMuncher.Editor
{
    public static class PlayerBonusVerification
    {
        [MenuItem("Horde Muncher/Verify Player Bonuses")]
        public static void RunMenu() => UnityEngine.Debug.Log(Run());

        public static string Run()
        {
            using var world = new World("Player bonus checks");
            EntityManager entityManager = world.EntityManager;
            Entity player = entityManager.CreateEntity(typeof(ArenaPlayer),
                typeof(Simulate),
                typeof(PlayerInput),
                typeof(PlayerMovement),
                typeof(LocalTransform),
                typeof(CombatHealth),
                typeof(PlayerBonuses),
                typeof(WaveStatus));
            entityManager.SetComponentData(player, LocalTransform.Identity);
            entityManager.SetComponentData(player, new PlayerInput
            {
                Move = new float2(1, 0)
            });
            entityManager.SetComponentData(player, new PlayerMovement
            {
                Speed = 7,
                HalfExtent = 18.5f
            });
            entityManager.SetComponentData(player, new CombatHealth
            {
                Current = 100
            });
            entityManager.SetComponentData(player, new PlayerBonuses
            {
                MoveSpeed = 0.1f
            });
            entityManager.SetComponentData(player, new WaveStatus
            {
                Phase = WavePhase.Spawning
            });
            SimulationSystemGroup group = world.CreateSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(world.GetOrCreateSystem<PlayerMovementSystem>());
            world.SetTime(new TimeData(1, 1));
            group.Update();
            entityManager.CompleteAllTrackedJobs();
            if (math.abs(entityManager.GetComponentData<LocalTransform>(player).Position.x - 7.7f) > 0.001f)
            {
                throw new Exception("Movement bonus did not apply");
            }

            entityManager.SetComponentData(player, new WaveStatus
            {
                Phase = WavePhase.Shop
            });
            world.SetTime(new TimeData(2, 1));
            group.Update();
            entityManager.CompleteAllTrackedJobs();
            if (math.abs(entityManager.GetComponentData<LocalTransform>(player).Position.x - 7.7f) > 0.001f)
            {
                throw new Exception("Player moved inside shop");
            }

            entityManager.SetComponentData(player, new WaveStatus
            {
                Phase = WavePhase.Spawning
            });
            entityManager.SetComponentData(player, new CombatHealth());
            world.SetTime(new TimeData(3, 1));
            group.Update();
            entityManager.CompleteAllTrackedJobs();
            if (math.abs(entityManager.GetComponentData<LocalTransform>(player).Position.x - 7.7f) > 0.001f)
            {
                throw new Exception("Dead player moved");
            }

            return "Passed: real movement job, speed bonus, shop pause and dead-player pause.";
        }
    }
}
