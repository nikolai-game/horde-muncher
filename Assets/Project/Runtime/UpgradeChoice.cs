using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct UpgradeChoice : IBufferElementData
    {
        [GhostField]
        public BonusKind Stat;
        [GhostField]
        public StatUpgradeRarity Rarity;
        [GhostField]
        public int Points;
    }
}
