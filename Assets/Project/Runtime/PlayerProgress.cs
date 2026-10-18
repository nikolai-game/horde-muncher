using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct PlayerProgress : IComponentData
    {
        [GhostField]
        public int Level;
        [GhostField]
        public int Xp;
        [GhostField]
        public int NextLevelXp;
        [GhostField]
        public int PendingUpgrades;
        [GhostField]
        public int CollectedItems;
        [GhostField]
        public bool Ready;
        [GhostField]
        public bool RestartReady;
        [GhostField]
        public uint Revision;
        [GhostField]
        public int Rerolls;
        [GhostField]
        public int UpgradeRerolls;
        [GhostField]
        public float PriceMultiplier;
        [GhostField]
        public ShopResult Result;
        public bool Initialized;
        public int ShopWave;
        public uint RandomState;
    }
}
