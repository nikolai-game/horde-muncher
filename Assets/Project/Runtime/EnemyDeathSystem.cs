using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    [UpdateBefore(typeof(WeaponFireSystem))]
    public partial class EnemyDeathSystem : SystemBase
    {
        private const float RESOURCE_GROUND_HEIGHT = 0.25f;
        private const float ITEM_DROP_HORIZONTAL_OFFSET = 0.5f;
        private const float ITEM_DROP_HEIGHT = 0.4f;

        private const float RESOURCE_DROP_SPACING = 0.4f;
        private const float GOLDEN_ANGLE_RADIANS = 2.3999632f;

        protected override void OnCreate()
        {
            RequireForUpdate<ArenaConfiguration>();
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.TryGetSingleton(out WaveProgress progress) && progress.IsPaused)
            {
                return;
            }

            ArenaConfiguration arenaConfiguration = SystemAPI.GetSingleton<ArenaConfiguration>();
            bool hasEconomyConfiguration = SystemAPI.TryGetSingleton(out EconomyConfig economyConfiguration);
            using var entityCommands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (enemyDeath, enemyData, enemyTransform, enemyEntity) in
                SystemAPI.Query<RefRO<EnemyDeath>, RefRO<ArenaEnemy>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                Entity killerEntity = enemyDeath.ValueRO.Killer;
                if (EntityManager.Exists(killerEntity) && EntityManager.HasComponent<CombatWallet>(killerEntity))
                {
                    CombatWallet killerWallet = EntityManager.GetComponentData<CombatWallet>(killerEntity);
                    killerWallet.Kills++;
                    EntityManager.SetComponentData(killerEntity, killerWallet);
                    if (hasEconomyConfiguration && EntityManager.HasComponent<PlayerProgress>(killerEntity))
                    {
                        EconomyRules.AwardExperience(EntityManager, killerEntity, enemyData.ValueRO.Experience, economyConfiguration);
                    }
                }

                if (arenaConfiguration.ResourcePrefab != Entity.Null)
                {
                    int resourceCount = math.max(1, enemyData.ValueRO.ResourceReward);
                    for (int resourceIndex = 0; resourceIndex < resourceCount; resourceIndex++)
                    {
                        float angle = resourceIndex * GOLDEN_ANGLE_RADIANS;
                        float radius = resourceCount > 1 ? RESOURCE_DROP_SPACING * math.sqrt(resourceIndex + 1) : 0;
                        LocalTransform resourceTransform = EntityManager.GetComponentData<LocalTransform>(arenaConfiguration.ResourcePrefab);
                        resourceTransform.Position = enemyTransform.ValueRO.Position
                            + new float3(math.cos(angle) * radius, 0, math.sin(angle) * radius);
                        resourceTransform.Position.y = RESOURCE_GROUND_HEIGHT;
                        Entity resourceEntity = entityCommands.Instantiate(arenaConfiguration.ResourcePrefab);
                        entityCommands.SetComponent(resourceEntity, resourceTransform);
                        entityCommands.SetComponent(resourceEntity, new ResourcePickup
                        {
                            Value = 1
                        });
                    }
                }

                TryDropItem(entityCommands, arenaConfiguration.ResourcePrefab, killerEntity, enemyTransform.ValueRO.Position);
                entityCommands.DestroyEntity(enemyEntity);
            }

            entityCommands.Playback(EntityManager);
        }

        private void TryDropItem(EntityCommandBuffer entityCommands, Entity resourcePrefab, Entity killer, float3 position)
        {
            if (resourcePrefab == Entity.Null || !EntityManager.HasComponent<PlayerBonuses>(killer)
                || !EntityManager.HasComponent<PlayerProgress>(killer) || !SystemAPI.TryGetSingletonEntity<EconomyConfig>(out Entity configEntity)
                || !EntityManager.HasBuffer<CatalogEntry>(configEntity))
            {
                return;
            }

            PlayerProgress progress = EntityManager.GetComponentData<PlayerProgress>(killer);
            var random = new Random(progress.RandomState == 0 ? 1u : progress.RandomState);
            int luck = EntityManager.GetComponentData<PlayerBonuses>(killer).Luck;
            bool dropped = ItemDropRules.TryRollItem(EntityManager.GetBuffer<CatalogEntry>(configEntity, true), luck, ref random,
                out int catalogId);
            progress.RandomState = random.state;
            EntityManager.SetComponentData(killer, progress);
            if (!dropped)
            {
                return;
            }

            Entity prefab = EntityManager.HasComponent<ItemDropPrefab>(resourcePrefab)
                ? EntityManager.GetComponentData<ItemDropPrefab>(resourcePrefab).Value : resourcePrefab;
            Entity itemEntity = entityCommands.Instantiate(prefab);
            LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(prefab);
            transform.Position = position + new float3(ITEM_DROP_HORIZONTAL_OFFSET, 0, 0);
            transform.Position.y = ITEM_DROP_HEIGHT;
            entityCommands.SetComponent(itemEntity, transform);
            entityCommands.SetComponent(itemEntity, new ResourcePickup
            {
                IsItem = true,
                ItemCatalogId = catalogId
            });
        }
    }
}
