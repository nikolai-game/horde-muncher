using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HordeMuncher
{
    public partial struct PlayerMovementSystem
    {
        [BurstCompile]
        [WithAll(typeof(ArenaPlayer), typeof(Simulate))]
        private partial struct MovePlayersJob : IJobEntity
        {
            private const float MIN_SPEED_MULTIPLIER = 0.1f;

            public float DeltaTime;

            private void Execute(ref LocalTransform transform,
                in PlayerInput input,
                in PlayerMovement movement,
                in CombatHealth playerHealth,
                in PlayerBonuses playerBonuses,
                in WaveStatus waveStatus)
            {
                if (waveStatus.IsPaused || playerHealth.Current <= 0 || waveStatus.Phase != WavePhase.Spawning)
                {
                    return;
                }

                float3 nextPosition = ArenaMovement.CalculateNextPosition(transform.Position,
                    input.Move,
                    movement.Speed * math.max(MIN_SPEED_MULTIPLIER, 1 + playerBonuses.MoveSpeed),
                    movement.HalfExtent,
                    DeltaTime);
                transform.Position = nextPosition;
            }
        }
    }
}
