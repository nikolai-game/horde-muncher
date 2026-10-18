using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct PlayerBonuses : IComponentData
    {
        [GhostField]
        public float Damage;
        [GhostField]
        public float MeleeDamage;
        [GhostField]
        public float RangedDamage;
        [GhostField]
        public float CriticalChance;
        [GhostField]
        public float CriticalDamageMultiplier;
        [GhostField]
        public float AttackSpeed;
        [GhostField]
        public float MoveSpeed;
        [GhostField]
        public float Range;
        [GhostField]
        public float Pickup;
        [GhostField]
        public float Regeneration;
        [GhostField]
        public int Armor;
        [GhostField]
        public int Luck;
        public float RegenRemainder;
    }
}
