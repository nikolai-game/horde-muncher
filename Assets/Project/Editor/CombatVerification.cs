using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace HordeMuncher.Editor
{
    public static partial class CombatVerification
    {
        public static string Capture()
        {
            var result = new List<CombatWorldSnapshot>();
            foreach (World world in World.All)
            {
                if (!world.IsCreated || (!world.IsClient() && !world.IsServer()))
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using EntityQuery enemies = entityManager.CreateEntityQuery(typeof(ArenaEnemy));
                using EntityQuery drops = entityManager.CreateEntityQuery(typeof(ResourcePickup));
                using EntityQuery players = entityManager.CreateEntityQuery(typeof(ArenaPlayer),
                    typeof(CombatHealth),
                    typeof(CombatWallet),
                    typeof(GhostOwner),
                    typeof(WeaponShot));
                using NativeArray<Entity> entities = players.ToEntityArray(Allocator.Temp);
                var states = new List<CombatPlayerSnapshot>();
                foreach (Entity entity in entities)
                {
                    CombatWallet wallet = entityManager.GetComponentData<CombatWallet>(entity);
                    states.Add(new CombatPlayerSnapshot
                    {
                        NetworkId = entityManager.GetComponentData<GhostOwner>(entity).NetworkId,
                        Health = entityManager.GetComponentData<CombatHealth>(entity).Current,
                        Resources = wallet.Resources,
                        Kills = wallet.Kills,
                        ShotSequence = entityManager.GetComponentData<WeaponShot>(entity).Sequence
                    });
                }

                result.Add(new CombatWorldSnapshot
                {
                    WorldName = world.Name,
                    EnemyCount = enemies.CalculateEntityCount(),
                    ResourceCount = drops.CalculateEntityCount(),
                    Players = states.ToArray()
                });
            }

            return JsonUtility.ToJson(new CombatSnapshot
            {
                Worlds = result.ToArray()
            }, true);
        }
    }
}
