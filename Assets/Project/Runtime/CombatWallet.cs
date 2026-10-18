using Unity.Entities;
using Unity.NetCode;

namespace HordeMuncher
{
    public struct CombatWallet : IComponentData
    {
        [GhostField]
        public int Resources;
        [GhostField]
        public int Kills;
        [GhostField]
        public int BankedResources;
        [GhostField]
        public uint BonusPickupSequence;
    }
}
