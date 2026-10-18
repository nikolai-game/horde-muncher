using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace HordeMuncher.Editor
{
    public static partial class ArenaVerification
    {
        public static string Capture()
        {
            var worlds = new List<ArenaWorldSnapshot>();
            foreach (World world in World.All)
            {
                if (!world.IsCreated || (!world.IsClient() && !world.IsServer()))
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using EntityQuery playerQuery = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(GhostOwner), typeof(LocalTransform));
                using NativeArray<Entity> playerEntities = playerQuery.ToEntityArray(Allocator.Temp);
                using EntityQuery connections = entityManager.CreateEntityQuery(typeof(NetworkStreamInGame));
                var players = new List<ArenaPlayerSnapshot>();
                foreach (Entity entity in playerEntities)
                {
                    float3 playerPosition = entityManager.GetComponentData<LocalTransform>(entity).Position;
                    players.Add(new ArenaPlayerSnapshot
                    {
                        NetworkId = entityManager.GetComponentData<GhostOwner>(entity).NetworkId,
                        PositionX = playerPosition.x,
                        PositionZ = playerPosition.z,
                        IsLocal = entityManager.HasComponent<GhostOwnerIsLocal>(entity)
                        && entityManager.IsComponentEnabled<GhostOwnerIsLocal>(entity)
                    });
                }

                worlds.Add(new ArenaWorldSnapshot
                {
                    WorldName = world.Name,
                    ConnectionCount = connections.CalculateEntityCount(),
                    Players = players.OrderBy(playerState => playerState.NetworkId).ToArray()
                });
            }

            return JsonUtility.ToJson(new ArenaSnapshot
            {
                Worlds = worlds.ToArray()
            }, true);
        }

        private static World GetRequiredWorld(string worldName)
        {
            foreach (World world in World.All)
            {
                if (world.Name == worldName)
                {
                    return world;
                }
            }

            throw new InvalidOperationException("World not found: " + worldName);
        }

        public static void SetInput(string worldName, float positionX, float positionZ)
        {
            World world = GetRequiredWorld(worldName);
            VerificationInputSystem system = world.GetExistingSystemManaged<VerificationInputSystem>();
            if (system == null)
            {
                system = world.CreateSystemManaged<VerificationInputSystem>();
                GhostInputSystemGroup group = world.GetExistingSystemManaged<GhostInputSystemGroup>();
                group.AddSystemToUpdateList(system);
                group.SortSystems();
            }

            system.Move = new float2(positionX, positionZ);
        }

        public static void DisconnectSecondClient()
        {
            World world = GetRequiredWorld("ArenaClient2");
            using EntityQuery playerQuery = world.EntityManager.CreateEntityQuery(typeof(NetworkId));
            using NativeArray<Entity> playerEntities = playerQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in playerEntities)
            {
                world.EntityManager.AddComponentData(entity, new NetworkStreamRequestDisconnect
                {
                    Reason = NetworkStreamDisconnectReason.ConnectionClose
                });
            }
        }

        [MenuItem("Horde Muncher/Verify Movement Rules")]
        public static void VerifyMovementRules()
        {
            void Require(bool condition, string message)
            {
                if (!condition)
                {
                    throw new Exception(message);
                }
            }

            float3 diagonal = ArenaMovement.CalculateNextPosition(new float3(0, 1, 0), new float2(1, 1), 7, 18.5f, 1);
            Require(math.abs(math.length(diagonal.xz) - 7) < 0.001f, "Diagonal speed boost");
            float3 boundary = ArenaMovement.CalculateNextPosition(new float3(18, 1, -18), new float2(100, -100), 7, 18.5f, 1);
            Require(math.all(boundary.xz == new float2(18.5f, -18.5f)), "Arena boundary violated");
            float3 invalid = ArenaMovement.CalculateNextPosition(new float3(3, 1, 4), new float2(float.NaN, float.PositiveInfinity), 7, 18.5f, 1);
            Require(math.all(invalid == new float3(3, 1, 4)), "Invalid input corrupted position");
            float3 singleStepPosition = ArenaMovement.CalculateNextPosition(new float3(0, 1, 0), new float2(0.5f, 0), 7, 18.5f, 1);
            var subdividedPosition = new float3(0, 1, 0);
            for (int tickIndex = 0; tickIndex < 60; tickIndex++)
            {
                subdividedPosition = ArenaMovement.CalculateNextPosition(subdividedPosition, new float2(0.5f, 0), 7, 18.5f, 1f / 60);
            }

            Require(math.distance(singleStepPosition, subdividedPosition) < 0.001f, "Movement depends on tick subdivision");
            Debug.Log("Arena verification: 4 movement rules passed.");
        }
    }
}
