using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace HordeMuncher.Editor
{
    public static partial class EnemyVerification
    {
        public static string Capture()
        {
            var result = new List<EnemyWorldSnapshot>();
            foreach (World world in World.All)
            {
                if (!world.IsCreated || (!world.IsServer() && !world.IsClient()))
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using EntityQuery enemies = entityManager.CreateEntityQuery(typeof(ArenaEnemy), typeof(LocalTransform));
                using EntityQuery players = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(LocalTransform));
                using NativeArray<LocalTransform> enemyPositions = enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                using NativeArray<LocalTransform> playerPositions = players.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                EnemyWorldSnapshot item = new EnemyWorldSnapshot
                {
                    WorldName = world.Name,
                    EnemyCount = enemyPositions.Length,
                    PlayerCount = playerPositions.Length
                };
                foreach (LocalTransform enemy in enemyPositions)
                {
                    if (!math.all(math.isfinite(enemy.Position)))
                    {
                        item.InvalidPositionCount++;
                    }

                    if (playerPositions.Length == 0)
                    {
                        continue;
                    }

                    float nearest = float.MaxValue;
                    foreach (LocalTransform player in playerPositions)
                    {
                        nearest = math.min(nearest, math.distance(enemy.Position.xz, player.Position.xz));
                    }

                    item.MeanNearestPlayerDistance += nearest;
                }

                if (item.EnemyCount > 0)
                {
                    item.MeanNearestPlayerDistance /= item.EnemyCount;
                }

                result.Add(item);
            }

            return JsonUtility.ToJson(new EnemySnapshot
            {
                Worlds = result.ToArray()
            }, true);
        }
    }
}
