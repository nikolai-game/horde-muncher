using UnityEngine;

namespace HordeMuncher
{
    public sealed partial class PlayerWeaponVisuals
    {
        private sealed class WeaponVisual
        {
            public int CatalogId;
            public GameObject Root;
            public uint AttackSequence;
            public float StrikeTime;
        }
    }
}
