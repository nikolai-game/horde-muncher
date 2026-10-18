using UnityEngine;

namespace HordeMuncher
{
    [CreateAssetMenu(menuName = "Horde Muncher/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Min(0.01f), Tooltip("Радиус тела в мировых метрах; учитывайте масштаб модели.")]
        public float Radius = 0.325f;
        [Min(0), Tooltip("Дополнительный зазор между поверхностями соседей.")]
        public float SeparationGap = 0.2f;
        [Min(0), Tooltip("Скорость раздвижения в м/с, не физическая сила.")]
        public float SeparationSpeed = 12;
        [Min(0)]
        public float MoveSpeed = 2.5f;
        [Min(0), Tooltip("Зазор от поверхности врага до центра игрока.")]
        public float TargetGap = 0.475f;
        [Min(0)]
        public float SpawnHeight = 0.65f;
        [Min(1)]
        public int Health = 30;
        [Min(0)]
        public int Experience = 1;
        [Min(1), Tooltip("Количество денег в ресурсе, выпадающем после смерти врага.")]
        public int ResourceReward = 1;
        [Min(0)]
        public int ContactDamage = 1;
        [Min(0.01f)]
        public float ContactCooldown = 0.75f;
    }
}
