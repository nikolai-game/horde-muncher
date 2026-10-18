using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public static class ClientArenaRoster
    {
        public static List<ClientArenaPlayer> Read(IReadOnlyList<World> localWorlds)
        {
            var players = new SortedDictionary<int, ClientArenaPlayer>();
            for (int localIndex = 0; localIndex < localWorlds.Count; localIndex++)
            {
                World world = localWorlds[localIndex];
                if (!world.IsCreated)
                {
                    continue;
                }

                EntityManager manager = world.EntityManager;
                using EntityQuery connections = manager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<NetworkId>(), ComponentType.ReadOnly<NetworkStreamInGame>() },
                    None = new[] { ComponentType.ReadOnly<NetworkStreamRequestDisconnect>() }
                });
                if (connections.CalculateEntityCount() != 1)
                {
                    continue;
                }

                int localNumber = connections.GetSingleton<NetworkId>().Value;
                using EntityQuery query = manager.CreateEntityQuery(typeof(ArenaPlayer), typeof(GhostOwner));
                using NativeArray<GhostOwner> owners = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                foreach (GhostOwner owner in owners)
                {
                    int number = owner.NetworkId;
                    if (number <= 0)
                    {
                        continue;
                    }

                    bool isLocal = number == localNumber;
                    if (isLocal || !players.ContainsKey(number))
                    {
                        players[number] = new ClientArenaPlayer(world, number, isLocal ? localIndex : -1);
                    }
                }
            }

            var result = new List<ClientArenaPlayer>();
            foreach (ClientArenaPlayer player in players.Values)
            {
                if (result.Count == ArenaLimits.MAX_PLAYERS)
                {
                    break;
                }

                result.Add(player);
            }

            return result;
        }
    }
}
