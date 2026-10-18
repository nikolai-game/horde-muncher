using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(NetworkReceiveSystemGroup))]
    [UpdateBefore(typeof(ArenaPauseServerSystem))]
    public partial class IncomingArenaRpcSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            using (EntityQuery rejected = EntityManager.CreateEntityQuery(typeof(RejectedArenaConnection)))
            using (NativeArray<Entity> connections = rejected.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity connection in connections)
                {
                    if (now >= EntityManager.GetComponentData<RejectedArenaConnection>(connection).DisconnectAt
                        && !EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
                    {
                        EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
                    }
                }
            }

            using EntityQuery received = EntityManager.CreateEntityQuery(typeof(ReceiveRpcCommandRequest));
            using NativeArray<Entity> requests = received.ToEntityArray(Allocator.Temp);
            foreach (Entity request in requests)
            {
                Entity connection = EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
                if (!EntityManager.HasComponent<NetworkId>(connection)
                    || EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection)
                    || EntityManager.HasComponent<RejectedArenaConnection>(connection))
                {
                    EntityManager.DestroyEntity(request);
                    continue;
                }

                IncomingRpcBudget budget = EntityManager.HasComponent<IncomingRpcBudget>(connection)
                    ? EntityManager.GetComponentData<IncomingRpcBudget>(connection)
                    : new IncomingRpcBudget { Tokens = IncomingRpcBudget.BURST_CAPACITY, UpdatedAt = now };
                budget.Tokens = Math.Min(IncomingRpcBudget.BURST_CAPACITY,
                    budget.Tokens + Math.Max(0, now - budget.UpdatedAt) * IncomingRpcBudget.REQUESTS_PER_SECOND);
                budget.UpdatedAt = now;
                bool withinBudget = budget.Tokens >= 1;
                budget.Tokens = Math.Max(0, budget.Tokens - 1);
                EntityManager.AddComponentData(connection, budget);
                if (!withinBudget)
                {
                    EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
                }

                bool isJoin = EntityManager.HasComponent<JoinArenaRequest>(request);
                bool isGameCommand = EntityManager.HasComponent<LobbySelectionRpc>(request)
                    || EntityManager.HasComponent<LobbyReadyRpc>(request) || EntityManager.HasComponent<SetPauseRpc>(request)
                    || EntityManager.HasComponent<ShopRequest>(request) || EntityManager.HasComponent<ShopNavigationRpc>(request);
                bool joined = EntityManager.HasComponent<LobbyMember>(connection)
                    && EntityManager.HasComponent<NetworkStreamInGame>(connection);
                if (!withinBudget || !(isJoin && !joined || isGameCommand && joined))
                {
                    EntityManager.DestroyEntity(request);
                }
            }
        }
    }
}
