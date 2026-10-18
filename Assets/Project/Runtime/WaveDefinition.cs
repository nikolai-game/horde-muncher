using System;
using UnityEngine;

namespace HordeMuncher
{
    [Serializable]
    public sealed class WaveDefinition
    {
        [Min(0.1f)]
        public float Duration = 20;
        [Tooltip("X: доля времени волны 0–1. Y: врагов в секунду.")]
        public AnimationCurve SpawnRate = AnimationCurve.Linear(0, 0.5f, 1, 3);
        [Tooltip("Равновероятный выбор записи для каждого спавна. Повторы префаба увеличивают его долю.")]
        public GameObject[] EnemyPrefabs;
        [Tooltip("Босс появляется после таймера волны; для завершения волны его нужно победить.")]
        public GameObject BossPrefab;
    }
}
