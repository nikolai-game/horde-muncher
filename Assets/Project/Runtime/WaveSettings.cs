using UnityEngine;

namespace HordeMuncher
{
    [CreateAssetMenu(menuName = "Horde Muncher/Wave Settings")]
    public sealed class WaveSettings : ScriptableObject
    {
        public Vector2 MapCenter;
        public Vector2 MapHalfExtents = new Vector2(19, 19);
        [Min(0)]
        public float SafeDistanceFromPlayers = 6;
        [Min(1)]
        public int MaxAlive = 500;
        [Min(1)]
        public int MaxSpawnsPerTick = 16;
        [Min(1)]
        public int PositionAttempts = 24;
        [Tooltip("0: случайный seed каждого забега; ненулевой: воспроизводимые спавны.")]
        public uint Seed;
        [Min(0)]
        public float CompletionPauseDuration = 3;
        [Min(0), Tooltip("Прибавка здоровья за волну в долях базового здоровья: 0.18 = +18%, без сложного процента.")]
        public float EnemyHealthGrowthPerWave = 0.18f;
        [Min(0), Tooltip("Прибавка урона за волну в долях базового урона: 0.08 = +8%, с округлением до ближайшего целого.")]
        public float EnemyDamageGrowthPerWave = 0.08f;
        [Min(0), Tooltip("Прибавка скорости за волну в долях базовой скорости; суммарно не более 20%.")]
        public float EnemySpeedGrowthPerWave = 0.02f;
        public WaveDefinition[] Waves =
        {
            new WaveDefinition
            {
                Duration = 20
            },
            new WaveDefinition
            {
                Duration = 30,
                SpawnRate = AnimationCurve.Linear(0, 1, 1, 5)
            },
            new WaveDefinition
            {
                Duration = 60,
                SpawnRate = AnimationCurve.Linear(0, 2, 1, 8)
            }
        };
    }
}
