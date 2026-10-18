using Unity.Entities;
using UnityEngine;

namespace HordeMuncher
{
    public sealed partial class PlayerAuthoring
    {
        private sealed class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                Entity playerEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<ArenaPlayer>(playerEntity);
                AddComponent<PlayerInput>(playerEntity);
                AddComponent(playerEntity, new PlayerMovement
                {
                    Speed = authoring.Speed,
                    HalfExtent = authoring.HalfExtent
                });
                AddComponent(playerEntity, new CombatHealth
                {
                    Current = authoring.Health,
                    Maximum = authoring.Health
                });
                AddComponent(playerEntity, new AutoWeapon
                {
                    Damage = Mathf.Max(1, authoring.Damage),
                    Interval = Mathf.Max(0.05f, authoring.ShotInterval),
                    Range = Mathf.Max(0.1f, authoring.ShotRange)
                });
                AddComponent<CombatWallet>(playerEntity);
                AddComponent<WeaponShot>(playerEntity);
                AddComponent<WaveStatus>(playerEntity);
                AddComponent<PlayerProgress>(playerEntity);
                AddComponent(playerEntity, new PlayerBonuses
                {
                    CriticalChance = Mathf.Clamp01(authoring.CriticalChance),
                    CriticalDamageMultiplier = Mathf.Max(1, authoring.CriticalDamageMultiplier)
                });
                AddBuffer<EquippedWeapon>(playerEntity);
                AddBuffer<OwnedItem>(playerEntity);
                AddBuffer<PendingItemReward>(playerEntity);
                AddBuffer<ShopOffer>(playerEntity);
                AddBuffer<UpgradeChoice>(playerEntity);
            }
        }
    }
}
