using System.Diagnostics;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher.Tests
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(NetworkReceiveSystemGroup))]
    public partial class ServerTickMeasurementStartSystem : SystemBase
    {
        public long StartedAt { get; private set; }

        protected override void OnUpdate()
        {
            StartedAt = Stopwatch.GetTimestamp();
        }
    }
}
