using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ClientCombatHealthChangesSystem : SystemBase
    {
        private const int MAX_PENDING_CHANGES = 256;

        private readonly Queue<CombatHealthChangeRpc> _pendingChanges = new Queue<CombatHealthChangeRpc>();

        public bool TryDequeue(out CombatHealthChangeRpc healthChange)
        {
            return _pendingChanges.TryDequeue(out healthChange);
        }

        protected override void OnUpdate()
        {
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (healthChange, message) in
                SystemAPI.Query<RefRO<CombatHealthChangeRpc>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                if (_pendingChanges.Count >= MAX_PENDING_CHANGES)
                {
                    _pendingChanges.Dequeue();
                }

                _pendingChanges.Enqueue(healthChange.ValueRO);
                commands.DestroyEntity(message);
            }

            commands.Playback(EntityManager);
        }
    }
}
