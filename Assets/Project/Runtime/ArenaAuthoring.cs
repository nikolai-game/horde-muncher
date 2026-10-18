using UnityEngine;

namespace HordeMuncher
{
    public sealed partial class ArenaAuthoring : MonoBehaviour
    {
        public GameObject PlayerPrefab;
        [Range(2, 4)]
        public int MaxPlayers = 4;
        [Min(4)]
        public float SharedViewMaxDistance = 22;
        public GameObject EnemyPrefab;
        public WaveSettings Waves;
        public EconomySettings Economy;
        public GameObject ResourcePrefab;
        public GameObject ProjectilePrefab;
    }
}
