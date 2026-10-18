using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace HordeMuncher
{
    public static class ArenaRunRules
    {
        public const float RESOURCE_COLLECTION_DURATION = 2f;

        public static float3 CalculatePlayerSpawnPosition(int networkId, int maxPlayers) =>
            new float3(((networkId - 1) % math.max(1, maxPlayers) % 2) * 4 - 2, 1, ((networkId - 1) % math.max(1, maxPlayers) / 2) * 4 - 2);

        public static void ReviveDefeatedPlayers(EntityManager entityManager)
        {
            using EntityQuery query = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(CombatHealth), typeof(LocalTransform));
            using NativeArray<Entity> playerEntities = query.ToEntityArray(Allocator.Temp);
            float3 survivorPosition = float3.zero;
            bool hasSurvivor = false;
            foreach (Entity playerEntity in playerEntities)
            {
                if (entityManager.GetComponentData<CombatHealth>(playerEntity).Current > 0)
                {
                    survivorPosition = entityManager.GetComponentData<LocalTransform>(playerEntity).Position;
                    hasSurvivor = true;
                    break;
                }
            }

            if (!hasSurvivor)
            {
                return;
            }

            int revivedPlayerCount = 0;
            foreach (Entity playerEntity in playerEntities)
            {
                CombatHealth health = entityManager.GetComponentData<CombatHealth>(playerEntity);
                if (health.Current > 0)
                {
                    continue;
                }

                health.Current = math.max(1, health.Maximum);
                health.HurtCooldown = 0;
                entityManager.SetComponentData(playerEntity, health);
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
                int reviveSlot = revivedPlayerCount++;
                float2 reviveOffset = new float2(reviveSlot == 1 ? 0 : survivorPosition.x <= 0 ? 2 : -2,
                    reviveSlot == 0 ? 0 : survivorPosition.z <= 0 ? 2 : -2);
                transform.Position = survivorPosition + new float3(reviveOffset.x, 0, reviveOffset.y);
                if (entityManager.HasComponent<PlayerMovement>(playerEntity))
                {
                    float arenaHalfExtent = entityManager.GetComponentData<PlayerMovement>(playerEntity).HalfExtent;
                    transform.Position.xz = math.clamp(transform.Position.xz, new float2(-arenaHalfExtent), new float2(arenaHalfExtent));
                }

                entityManager.SetComponentData(playerEntity, transform);
            }
        }

        public static void CompleteWave(EntityManager entityManager)
        {
            using EntityQuery enemyQuery = entityManager.CreateEntityQuery(typeof(ArenaEnemy));
            using EntityQuery projectileQuery = entityManager.CreateEntityQuery(typeof(ArenaProjectile));
            using EntityQuery resourceQuery = entityManager.CreateEntityQuery(typeof(ResourcePickup));
            using EntityQuery recipientQuery = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(CombatWallet));
            using NativeArray<Entity> resourceEntities = resourceQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<Entity> recipientEntities = recipientQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity resourceEntity in resourceEntities)
            {
                ResourcePickup pickup = entityManager.GetComponentData<ResourcePickup>(resourceEntity);
                float3 position = entityManager.HasComponent<LocalTransform>(resourceEntity)
                    ? entityManager.GetComponentData<LocalTransform>(resourceEntity).Position : float3.zero;
                Entity collector = Entity.Null;
                float nearestDistanceSquared = float.MaxValue;
                foreach (Entity recipient in recipientEntities)
                {
                    float3 recipientPosition = entityManager.HasComponent<LocalTransform>(recipient)
                        ? entityManager.GetComponentData<LocalTransform>(recipient).Position : float3.zero;
                    float distanceSquared = math.distancesq(position, recipientPosition);
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearestDistanceSquared = distanceSquared;
                        collector = recipient;
                    }
                }

                pickup.Collector = collector;
                pickup.StoreInBag = true;
                pickup.AttractionStartPosition = position;
                pickup.AttractionElapsedTime = 0;
                entityManager.SetComponentData(resourceEntity, pickup);
            }

            DestroyEntitiesAndLinkedGroups(entityManager, enemyQuery);
            DestroyEntitiesAndLinkedGroups(entityManager, projectileQuery);
            if (recipientEntities.Length == 0)
            {
                DestroyEntitiesAndLinkedGroups(entityManager, resourceQuery);
            }
        }

        public static void RestartRun(EntityManager entityManager, Entity runEntity, ArenaConfiguration arenaConfiguration)
        {
            using EntityQuery enemyQuery = entityManager.CreateEntityQuery(typeof(ArenaEnemy));
            using EntityQuery projectileQuery = entityManager.CreateEntityQuery(typeof(ArenaProjectile));
            using EntityQuery resourceQuery = entityManager.CreateEntityQuery(typeof(ResourcePickup));
            DestroyEntitiesAndLinkedGroups(entityManager, enemyQuery);
            DestroyEntitiesAndLinkedGroups(entityManager, projectileQuery);
            DestroyEntitiesAndLinkedGroups(entityManager, resourceQuery);
            using EntityQuery query = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(PlayerProgress), typeof(GhostOwner));
            using NativeArray<Entity> playerEntities = query.ToEntityArray(Allocator.Temp);
            foreach (Entity playerEntity in playerEntities)
            {
                uint revision = entityManager.GetComponentData<PlayerProgress>(playerEntity).Revision + 1;
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
                int networkId = entityManager.GetComponentData<GhostOwner>(playerEntity).NetworkId;
                transform.Position = CalculatePlayerSpawnPosition(networkId, arenaConfiguration.MaxPlayers);
                entityManager.SetComponentData(playerEntity, transform);
                entityManager.SetComponentData(playerEntity, entityManager.GetComponentData<AutoWeapon>(arenaConfiguration.PlayerPrefab));
                int characterId = entityManager.GetComponentData<ArenaPlayer>(playerEntity).CharacterId;
                CharacterArchetypes.ApplyStartingStats(entityManager, playerEntity, arenaConfiguration.PlayerPrefab, characterId);
                entityManager.SetComponentData(playerEntity, new PlayerProgress
                {
                    Revision = revision
                });
                entityManager.SetComponentData(playerEntity, new CombatWallet());
                entityManager.SetComponentData(playerEntity, new WeaponShot());
                entityManager.SetComponentData(playerEntity, new PlayerInput());
                entityManager.SetComponentData(playerEntity, new WaveStatus());
                entityManager.GetBuffer<EquippedWeapon>(playerEntity).Clear();
                entityManager.GetBuffer<OwnedItem>(playerEntity).Clear();
                entityManager.GetBuffer<ShopOffer>(playerEntity).Clear();
                entityManager.GetBuffer<UpgradeChoice>(playerEntity).Clear();
                if (entityManager.HasBuffer<PendingItemReward>(playerEntity))
                {
                    entityManager.GetBuffer<PendingItemReward>(playerEntity).Clear();
                }
            }

            entityManager.SetComponentData(runEntity, new WaveProgress());
        }

        private static void DestroyEntitiesAndLinkedGroups(EntityManager entityManager, EntityQuery query)
        {
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            entityManager.DestroyEntity(entities);
        }
    }
}
