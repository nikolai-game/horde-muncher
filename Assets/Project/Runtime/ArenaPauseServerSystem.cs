using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    public partial class ArenaPauseServerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<WaveProgress>();
            RequireForUpdate<ArenaLobby>();
        }

        protected override void OnUpdate()
        {
            Entity runEntity = SystemAPI.GetSingletonEntity<WaveProgress>();
            WaveProgress progress = EntityManager.GetComponentData<WaveProgress>(runEntity);
            bool canPause = SystemAPI.GetSingleton<ArenaLobby>().Started
                && progress.Phase != WavePhase.Complete && progress.Phase != WavePhase.Defeat;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (pause, request, entity) in
                SystemAPI.Query<RefRO<SetPauseRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
            {
                Entity connection = request.ValueRO.SourceConnection;
                if (canPause && EntityManager.HasComponent<LobbyMember>(connection)
                    && EntityManager.HasComponent<NetworkStreamInGame>(connection)
                    && !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
                {
                    progress.IsPaused = pause.ValueRO.IsPaused;
                }

                commands.DestroyEntity(entity);
            }

            if (!canPause)
            {
                progress.IsPaused = false;
            }

            EntityManager.SetComponentData(runEntity, progress);
            foreach (RefRW<WaveStatus> status in SystemAPI.Query<RefRW<WaveStatus>>().WithAll<ArenaPlayer>())
            {
                status.ValueRW.IsPaused = progress.IsPaused;
            }

            commands.Playback(EntityManager);
        }
    }
}
