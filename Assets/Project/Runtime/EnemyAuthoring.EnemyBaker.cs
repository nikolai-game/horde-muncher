using Unity.Entities;
using UnityEngine;

namespace HordeMuncher
{
    public sealed partial class EnemyAuthoring
    {
        private sealed class EnemyBaker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                if (authoring.Definition == null)
                {
                    return;
                }

                DependsOn(authoring.Definition);
                EnemyDefinition enemyDefinition = authoring.Definition;
                AddComponent(GetEntity(TransformUsageFlags.Dynamic), new CombatHealth
                {
                    Current = Mathf.Max(1, enemyDefinition.Health),
                    Maximum = Mathf.Max(1, enemyDefinition.Health)
                });
                AddComponent(GetEntity(TransformUsageFlags.Dynamic), new ArenaEnemy
                {
                    Speed = Mathf.Max(0, enemyDefinition.MoveSpeed),
                    Radius = Mathf.Max(0.01f, enemyDefinition.Radius),
                    StopDistance = Mathf.Max(0.01f, enemyDefinition.Radius) + Mathf.Max(0, enemyDefinition.TargetGap),
                    SeparationGap = Mathf.Max(0, enemyDefinition.SeparationGap),
                    SpawnHeight = enemyDefinition.SpawnHeight,
                    SeparationSpeed = Mathf.Max(0, enemyDefinition.SeparationSpeed),
                    Experience = Mathf.Max(0, enemyDefinition.Experience),
                    ResourceReward = Mathf.Max(1, enemyDefinition.ResourceReward),
                    ContactDamage = Mathf.Max(0, enemyDefinition.ContactDamage),
                    ContactCooldown = Mathf.Max(0.01f, enemyDefinition.ContactCooldown)
                });
            }
        }
    }
}
