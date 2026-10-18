using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace HordeMuncher
{
    public sealed partial class PlayerWeaponVisuals : MonoBehaviour
    {
        private const float MELEE_STRIKE_VISUAL_SECONDS = 0.2f;
        private const float MIN_STRIKE_REACH = 0.5f;

        public EconomySettings Settings;
        private readonly Dictionary<(Entity, int), WeaponVisual> _weaponVisuals = new();
        private readonly HashSet<(Entity, int)> _visibleWeaponSlots = new();
        private readonly List<(Entity, int)> _removedWeaponSlots = new();
        private World _observedClientWorld;

        private void LateUpdate()
        {
            World clientWorld = null;
            foreach (World world in World.All)
            {
                if (world.IsCreated && world.IsClient())
                {
                    clientWorld = world;
                    break;
                }
            }

            if (clientWorld != _observedClientWorld)
            {
                ClearWeaponVisuals();
                _observedClientWorld = clientWorld;
            }

            if (clientWorld == null || Settings == null || Settings.Weapons == null)
            {
                return;
            }

            EntityManager entityManager = clientWorld.EntityManager;
            using EntityQuery playerQuery = entityManager.CreateEntityQuery(typeof(ArenaPlayer),
                typeof(EquippedWeapon),
                typeof(LocalTransform),
                typeof(CombatHealth));
            using NativeArray<Entity> playerEntities = playerQuery.ToEntityArray(Allocator.Temp);
            _visibleWeaponSlots.Clear();
            foreach (Entity playerEntity in playerEntities)
            {
                if (entityManager.GetComponentData<CombatHealth>(playerEntity).Current <= 0)
                {
                    continue;
                }

                DynamicBuffer<EquippedWeapon> weapons = entityManager.GetBuffer<EquippedWeapon>(playerEntity);
                float3 playerPosition = entityManager.HasComponent<LocalToWorld>(playerEntity)
                    ? entityManager.GetComponentData<LocalToWorld>(playerEntity).Position
                    : entityManager.GetComponentData<LocalTransform>(playerEntity).Position;
                for (int weaponSlot = 0; weaponSlot < weapons.Length; weaponSlot++)
                {
                    EquippedWeapon weapon = weapons[weaponSlot];
                    if (!Settings.TryGetWeapon(weapon.CatalogId, out WeaponData weaponDefinition))
                    {
                        continue;
                    }

                    if (weaponDefinition.ModelPrefab == null)
                    {
                        continue;
                    }

                    var weaponVisualKey = (playerEntity, weaponSlot);
                    _visibleWeaponSlots.Add(weaponVisualKey);
                    if (_weaponVisuals.TryGetValue(weaponVisualKey, out WeaponVisual weaponVisual) && weaponVisual.CatalogId != weapon.CatalogId)
                    {
                        Destroy(weaponVisual.Root);
                        _weaponVisuals.Remove(weaponVisualKey);
                        weaponVisual = null;
                    }

                    if (weaponVisual == null)
                    {
                        weaponVisual = new WeaponVisual
                        {
                            CatalogId = weapon.CatalogId,
                            Root = Instantiate(weaponDefinition.ModelPrefab, transform)
                        };
                        _weaponVisuals.Add(weaponVisualKey, weaponVisual);
                    }

                    if (weaponVisual.AttackSequence != weapon.AttackSequence)
                    {
                        weaponVisual.AttackSequence = weapon.AttackSequence;
                        weaponVisual.StrikeTime = Time.time;
                    }

                    float3 aimDirection = math.normalizesafe(weapon.Aim, new float3(0, 0, 1));
                    float3 mountPosition = WeaponLayout.CalculateMountPosition(playerPosition,
                        weaponSlot, weapons.Length, Settings.WeaponOrbitRadius, Settings.WeaponHeight);
                    quaternion weaponRotation = quaternion.LookRotationSafe(aimDirection, math.up());
                    float strikeProgress = (Time.time - weaponVisual.StrikeTime) / MELEE_STRIKE_VISUAL_SECONDS;
                    if (WeaponCombatRules.IsMelee(weaponDefinition.Weapon) && weaponVisual.AttackSequence > 0 && strikeProgress < 1)
                    {
                        float strikeExtension = math.sin(math.saturate(strikeProgress) * math.PI);
                        float rangeBonus = entityManager.HasComponent<PlayerBonuses>(playerEntity)
                            ? entityManager.GetComponentData<PlayerBonuses>(playerEntity).Range : 0;
                        float attackRange = WeaponCombatRules.CalculateAttackRange(weaponDefinition.Range, rangeBonus);
                        float strikeReach = math.max(MIN_STRIKE_REACH, attackRange - Settings.WeaponOrbitRadius);
                        mountPosition += aimDirection * (strikeExtension * strikeReach);
                        if (weaponDefinition.Weapon == WeaponKind.Axe)
                        {
                            weaponRotation = math.mul(weaponRotation, quaternion.RotateY(math.lerp(-1, 1, strikeProgress)));
                        }
                    }

                    weaponVisual.Root.transform.SetPositionAndRotation(mountPosition, weaponRotation);
                }
            }

            _removedWeaponSlots.Clear();
            foreach (KeyValuePair<(Entity, int), WeaponVisual> pair in _weaponVisuals)
            {
                if (!_visibleWeaponSlots.Contains(pair.Key))
                {
                    Destroy(pair.Value.Root);
                    _removedWeaponSlots.Add(pair.Key);
                }
            }

            foreach ((Entity, int) weaponVisualKey in _removedWeaponSlots)
            {
                _weaponVisuals.Remove(weaponVisualKey);
            }
        }

        private void OnDisable()
        {
            ClearWeaponVisuals();
            _observedClientWorld = null;
        }

        private void ClearWeaponVisuals()
        {
            foreach (WeaponVisual weaponVisual in _weaponVisuals.Values)
            {
                if (weaponVisual.Root != null)
                {
                    Destroy(weaponVisual.Root);
                }
            }

            _weaponVisuals.Clear();
        }
    }
}
