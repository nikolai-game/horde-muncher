using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HealthRegenerationSystem))]
    [UpdateAfter(typeof(WaveCompletionSystem))]
    public partial class ResourcePickupSystem : SystemBase
    {
        private const float PLAYER_BODY_RADIUS = 0.5f;

        private EntityQuery _playerQuery;
        private EntityQuery _resourceQuery;
        private Entity _lastResourceRecipient;

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(typeof(ArenaPlayer), typeof(CombatHealth), typeof(CombatWallet), typeof(LocalTransform));
            _resourceQuery = GetEntityQuery(typeof(ResourcePickup), typeof(LocalTransform));
            RequireForUpdate<ArenaConfiguration>();
        }

        protected override void OnUpdate()
        {
            bool collectingWaveRemainder = SystemAPI.TryGetSingleton(out WaveProgress waveProgress)
                && !waveProgress.IsPaused && waveProgress.Phase == WavePhase.WaveCleared;
            if (!collectingWaveRemainder && !CombatPhaseRules.IsActive(EntityManager))
            {
                return;
            }

            bool hasEconomyConfiguration = SystemAPI.TryGetSingleton(out EconomyConfig economyConfiguration);
            float attractionDuration = hasEconomyConfiguration ? math.max(0.01f, economyConfiguration.ResourceAttractionDuration) : 0.5f;
            DynamicBuffer<ResourceAttractionCurvePoint> attractionCurvePoints = default;
            if (hasEconomyConfiguration)
            {
                Entity configurationEntity = SystemAPI.GetSingletonEntity<EconomyConfig>();
                if (EntityManager.HasBuffer<ResourceAttractionCurvePoint>(configurationEntity))
                {
                    attractionCurvePoints = EntityManager.GetBuffer<ResourceAttractionCurvePoint>(configurationEntity, true);
                }
            }

            using NativeArray<Entity> playerEntities = _playerQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<Entity> resourceEntities = _resourceQuery.ToEntityArray(Allocator.Temp);
            using var entityCommands = new EntityCommandBuffer(Allocator.Temp);
            foreach (Entity resourceEntity in resourceEntities)
            {
                ResourcePickup resourcePickup = EntityManager.GetComponentData<ResourcePickup>(resourceEntity);
                LocalTransform resourceTransform = EntityManager.GetComponentData<LocalTransform>(resourceEntity);
                if (!resourcePickup.IsItem && !resourcePickup.StoreInBag && IsTouchingLivingPlayer(resourceTransform, playerEntities))
                {
                    CollectResource(resourcePickup, playerEntities);
                    entityCommands.DestroyEntity(resourceEntity);
                    continue;
                }

                if (resourcePickup.Collector != Entity.Null)
                {
                    if (EntityManager.HasComponent<CombatWallet>(resourcePickup.Collector)
                        && EntityManager.HasComponent<LocalTransform>(resourcePickup.Collector)
                        && EntityManager.HasComponent<CombatHealth>(resourcePickup.Collector)
                        && (resourcePickup.StoreInBag || EntityManager.GetComponentData<CombatHealth>(resourcePickup.Collector).Current > 0))
                    {
                        resourcePickup.AttractionElapsedTime += SystemAPI.Time.DeltaTime;
                        float duration = resourcePickup.StoreInBag ? ArenaRunRules.RESOURCE_COLLECTION_DURATION : attractionDuration;
                        float progress = math.saturate(resourcePickup.AttractionElapsedTime / duration);
                        float3 collectorPosition = EntityManager.GetComponentData<LocalTransform>(resourcePickup.Collector).Position;
                        resourceTransform.Position = math.lerp(resourcePickup.AttractionStartPosition, collectorPosition,
                            EvaluateAttractionProgress(attractionCurvePoints, progress));
                        EntityManager.SetComponentData(resourceEntity, resourceTransform);
                        EntityManager.SetComponentData(resourceEntity, resourcePickup);
                        if (progress >= 1)
                        {
                            if (resourcePickup.IsItem)
                            {
                                if (hasEconomyConfiguration)
                                {
                                    Entity configEntity = SystemAPI.GetSingletonEntity<EconomyConfig>();
                                    if (EntityManager.HasBuffer<CatalogEntry>(configEntity))
                                    {
                                        ItemDropRules.TryCollectItem(EntityManager, resourcePickup.Collector, resourcePickup.ItemCatalogId,
                                            EntityManager.GetBuffer<CatalogEntry>(configEntity, true));
                                    }
                                }

                                entityCommands.DestroyEntity(resourceEntity);
                                continue;
                            }

                            CollectResource(resourcePickup, playerEntities);
                            entityCommands.DestroyEntity(resourceEntity);
                        }

                        continue;
                    }

                    resourcePickup.Collector = Entity.Null;
                    resourcePickup.AttractionElapsedTime = 0;
                    EntityManager.SetComponentData(resourceEntity, resourcePickup);
                }

                float3 resourcePosition = resourceTransform.Position;
                Entity collectorEntity = Entity.Null;
                float nearestDistanceSquared = float.MaxValue;
                foreach (Entity playerEntity in playerEntities)
                {
                    if (!resourcePickup.StoreInBag && EntityManager.GetComponentData<CombatHealth>(playerEntity).Current <= 0)
                    {
                        continue;
                    }

                    float pickupRadius = 1.4f
                        + (EntityManager.HasComponent<PlayerBonuses>(playerEntity)
                        ? EntityManager.GetComponentData<PlayerBonuses>(playerEntity).Pickup
                        : 0);
                    float3 playerPosition = EntityManager.GetComponentData<LocalTransform>(playerEntity).Position;
                    float distanceSquared = math.distancesq(resourcePosition.xz, playerPosition.xz);
                    if ((!collectingWaveRemainder && distanceSquared > pickupRadius * pickupRadius)
                        || distanceSquared >= nearestDistanceSquared)
                    {
                        continue;
                    }

                    nearestDistanceSquared = distanceSquared;
                    collectorEntity = playerEntity;
                }

                if (collectorEntity == Entity.Null)
                {
                    if (collectingWaveRemainder)
                    {
                        entityCommands.DestroyEntity(resourceEntity);
                    }

                    continue;
                }

                resourcePickup.Collector = collectorEntity;
                resourcePickup.AttractionStartPosition = resourcePosition;
                resourcePickup.AttractionElapsedTime = 0;
                EntityManager.SetComponentData(resourceEntity, resourcePickup);
            }

            entityCommands.Playback(EntityManager);
        }

        private bool IsTouchingLivingPlayer(LocalTransform resourceTransform, NativeArray<Entity> playerEntities)
        {
            float contactDistance = PLAYER_BODY_RADIUS + resourceTransform.Scale * 0.5f;
            foreach (Entity playerEntity in playerEntities)
            {
                if (EntityManager.GetComponentData<CombatHealth>(playerEntity).Current <= 0)
                {
                    continue;
                }

                float3 playerPosition = EntityManager.GetComponentData<LocalTransform>(playerEntity).Position;
                if (math.distancesq(resourceTransform.Position.xz, playerPosition.xz) <= contactDistance * contactDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private void CollectResource(ResourcePickup resourcePickup, NativeArray<Entity> playerEntities)
        {
            Entity recipient = resourcePickup.StoreInBag ? resourcePickup.Collector : GetNextResourceRecipient(playerEntities);
            CombatWallet recipientWallet = EntityManager.GetComponentData<CombatWallet>(recipient);
            if (resourcePickup.StoreInBag)
            {
                recipientWallet.BankedResources += resourcePickup.Value;
            }
            else
            {
                _lastResourceRecipient = recipient;
                int bonusResources = math.min(resourcePickup.Value, recipientWallet.BankedResources);
                recipientWallet.Resources += resourcePickup.Value + bonusResources;
                recipientWallet.BankedResources -= bonusResources;
                if (bonusResources > 0)
                {
                    recipientWallet.BonusPickupSequence++;
                }
            }
            EntityManager.SetComponentData(recipient, recipientWallet);
        }

        private Entity GetNextResourceRecipient(NativeArray<Entity> playerEntities)
        {
            Entity firstLivingPlayer = Entity.Null;
            Entity nextLivingPlayer = Entity.Null;
            foreach (Entity playerEntity in playerEntities)
            {
                if (EntityManager.GetComponentData<CombatHealth>(playerEntity).Current <= 0)
                {
                    continue;
                }

                if (firstLivingPlayer == Entity.Null || playerEntity.Index < firstLivingPlayer.Index)
                {
                    firstLivingPlayer = playerEntity;
                }

                if (playerEntity.Index > _lastResourceRecipient.Index
                    && (nextLivingPlayer == Entity.Null || playerEntity.Index < nextLivingPlayer.Index))
                {
                    nextLivingPlayer = playerEntity;
                }
            }

            return nextLivingPlayer != Entity.Null ? nextLivingPlayer : firstLivingPlayer;
        }

        private static float EvaluateAttractionProgress(DynamicBuffer<ResourceAttractionCurvePoint> attractionCurvePoints, float progress)
        {
            if (!attractionCurvePoints.IsCreated || attractionCurvePoints.Length < 2)
            {
                return progress * progress;
            }

            float curvePosition = progress * (attractionCurvePoints.Length - 1);
            int curvePointIndex = math.min((int)curvePosition, attractionCurvePoints.Length - 2);
            return math.lerp(attractionCurvePoints[curvePointIndex].Progress, attractionCurvePoints[curvePointIndex + 1].Progress,
                curvePosition - curvePointIndex);
        }
    }
}
