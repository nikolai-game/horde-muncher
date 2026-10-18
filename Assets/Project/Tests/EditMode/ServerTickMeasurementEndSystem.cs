using System.Collections.Generic;
using System.Diagnostics;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(RpcSystem))]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class ServerTickMeasurementEndSystem : SystemBase
    {
        private int _ticks;
        public ServerTickMeasurementStartSystem Start { get; set; }
        public List<double> TickDurationsMilliseconds { get; } = new List<double>();
        public int MinimumEnemies { get; private set; } = int.MaxValue;
        public int MaximumEnemies { get; private set; }
        public int MaximumProjectiles { get; private set; }
        public float MinimumDeltaTime { get; private set; } = float.MaxValue;
        public float MaximumDeltaTime { get; private set; }

        protected override void OnUpdate()
        {
            EntityManager.CompleteAllTrackedJobs();
            long finishedAt = Stopwatch.GetTimestamp();
            if (++_ticks <= 30 || TickDurationsMilliseconds.Count >= 120)
            {
                return;
            }

            TickDurationsMilliseconds.Add((finishedAt - Start.StartedAt) * 1000.0 / Stopwatch.Frequency);
            using EntityQuery enemies = EntityManager.CreateEntityQuery(typeof(ArenaEnemy));
            using EntityQuery projectiles = EntityManager.CreateEntityQuery(typeof(ArenaProjectile));
            int count = enemies.CalculateEntityCount();
            MinimumEnemies = System.Math.Min(MinimumEnemies, count);
            MaximumEnemies = System.Math.Max(MaximumEnemies, count);
            MaximumProjectiles = System.Math.Max(MaximumProjectiles, projectiles.CalculateEntityCount());
            MinimumDeltaTime = System.Math.Min(MinimumDeltaTime, SystemAPI.Time.DeltaTime);
            MaximumDeltaTime = System.Math.Max(MaximumDeltaTime, SystemAPI.Time.DeltaTime);
        }
    }
}
