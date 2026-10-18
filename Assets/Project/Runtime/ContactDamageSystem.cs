using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    [UpdateBefore(typeof(HealthRegenerationSystem))]
    public partial class ContactDamageSystem : SystemBase
    {
        private const float PLAYER_BODY_RADIUS = 0.5f;

        private EntityQuery _enemyQuery;

        protected override void OnCreate()
        {
            _enemyQuery = GetEntityQuery(typeof(ArenaEnemy), typeof(CombatHealth), typeof(LocalTransform));
            RequireForUpdate<ArenaConfiguration>();
        }

        protected override void OnUpdate()
        {
            if (!CombatPhaseRules.IsActive(EntityManager))
            {
                return;
            }

            using NativeArray<Entity> enemyEntities = _enemyQuery.ToEntityArray(Allocator.Temp);
            using var entityCommands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (playerHealth, playerTransform, playerEntity) in
                SystemAPI.Query<RefRW<CombatHealth>, RefRO<LocalTransform>>().WithAll<ArenaPlayer>().WithEntityAccess())
            {
                if (playerHealth.ValueRO.Current <= 0)
                {
                    continue;
                }

                CombatHealth updatedHealth = playerHealth.ValueRO;
                updatedHealth.HurtCooldown = math.max(0, updatedHealth.HurtCooldown - SystemAPI.Time.DeltaTime);
                int playerArmor = EntityManager.HasComponent<PlayerBonuses>(playerEntity)
                    ? EntityManager.GetComponentData<PlayerBonuses>(playerEntity).Armor
                    : 0;
                if (updatedHealth.HurtCooldown == 0)
                {
                    foreach (Entity enemyEntity in enemyEntities)
                    {
                        ArenaEnemy enemyData = EntityManager.GetComponentData<ArenaEnemy>(enemyEntity);
                        LocalTransform enemyTransform = EntityManager.GetComponentData<LocalTransform>(enemyEntity);
                        float distanceSquared = math.distancesq(playerTransform.ValueRO.Position.xz, enemyTransform.Position.xz);
                        float contactDistance = enemyData.Radius + PLAYER_BODY_RADIUS;
                        if (EntityManager.GetComponentData<CombatHealth>(enemyEntity).Current <= 0 || enemyData.ContactDamage <= 0
                            || distanceSquared > contactDistance * contactDistance)
                        {
                            continue;
                        }

                        updatedHealth.Current = math.max(0, updatedHealth.Current - math.max(1, enemyData.ContactDamage - playerArmor));
                        CombatHealthChanges.Send(entityCommands,
                            playerTransform.ValueRO.Position,
                            updatedHealth.Current - playerHealth.ValueRO.Current,
                            true);
                        updatedHealth.HurtCooldown = enemyData.ContactCooldown;
                        break;
                    }
                }

                playerHealth.ValueRW = updatedHealth;
            }

            entityCommands.Playback(EntityManager);
        }
    }
}
