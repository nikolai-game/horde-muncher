using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<ArenaPlayer>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new MovePlayersJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
        }
    }
}
