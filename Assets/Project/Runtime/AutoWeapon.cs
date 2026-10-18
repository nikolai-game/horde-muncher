using Unity.Entities;

namespace HordeMuncher
{
    public struct AutoWeapon : IComponentData
    {
        public float Range;
        public float Interval;
        public int Damage;
        public float Cooldown;
    }
}
