using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ContactDamageSystem))]
    [UpdateBefore(typeof(ResourcePickupSystem))]
    public partial class HealthRegenerationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<ArenaConfiguration>();
        }

        protected override void OnUpdate()
        {
            if (!CombatPhaseRules.IsActive(EntityManager))
            {
                return;
            }

            using var entityCommands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (playerHealth, playerBonuses, playerTransform) in
                SystemAPI.Query<RefRW<CombatHealth>, RefRW<PlayerBonuses>, RefRO<LocalTransform>>().WithAll<ArenaPlayer>())
            {
                if (playerHealth.ValueRO.Current <= 0 || playerBonuses.ValueRO.Regeneration <= 0)
                {
                    continue;
                }

                PlayerBonuses regenerationBonuses = playerBonuses.ValueRO;
                regenerationBonuses.RegenRemainder += regenerationBonuses.Regeneration * SystemAPI.Time.DeltaTime;
                int restoredHealth = (int)regenerationBonuses.RegenRemainder;
                int previousHealth = playerHealth.ValueRO.Current;
                playerHealth.ValueRW.Current = math.min(playerHealth.ValueRO.Maximum, playerHealth.ValueRO.Current + restoredHealth);
                CombatHealthChanges.Send(entityCommands,
                    playerTransform.ValueRO.Position,
                    playerHealth.ValueRO.Current - previousHealth,
                    true);
                regenerationBonuses.RegenRemainder -= restoredHealth;
                playerBonuses.ValueRW = regenerationBonuses;
            }

            entityCommands.Playback(EntityManager);
        }
    }
}
