using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<NetworkStreamInGame>();

        public void OnUpdate(ref SystemState state)
        {
            LocalPlayerInput binding = SystemAPI.TryGetSingleton(out LocalPlayerInput localInput)
                ? localInput : new LocalPlayerInput { KeyboardLayout = 1 };
            float2 movementInput = ArenaLocalInput.Read(binding, Application.isFocused);
            foreach (RefRW<PlayerInput> input in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
            {
                input.ValueRW.Move = movementInput;
            }
        }
    }
}
