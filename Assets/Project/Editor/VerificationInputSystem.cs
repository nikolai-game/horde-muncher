using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HordeMuncher.Editor
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [UpdateAfter(typeof(PlayerInputSystem))]
    public partial class VerificationInputSystem : SystemBase
    {
        public float2 Move;

        protected override void OnUpdate()
        {
            foreach (RefRW<PlayerInput> input in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
            {
                input.ValueRW.Move = Move;
            }
        }
    }
}
