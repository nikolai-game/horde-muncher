using UnityEngine;

namespace HordeMuncher
{
    [DisallowMultipleComponent]
    public sealed partial class PlayerAuthoring : MonoBehaviour
    {
        public float Speed = 7;
        public float HalfExtent = 18.5f;
        [Min(1)]
        public int Health = 10;
        [Min(1)]
        public int Damage = 15;
        [Range(0, 1)]
        public float CriticalChance = 0.1f;
        [Min(1)]
        public float CriticalDamageMultiplier = 2;
        [Min(0.05f)]
        public float ShotInterval = 0.25f;
        [Min(0.1f)]
        public float ShotRange = 9;
    }
}
