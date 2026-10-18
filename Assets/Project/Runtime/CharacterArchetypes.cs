using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    public static class CharacterArchetypes
    {
        private const float STRONG_HEALTH_MULTIPLIER = 1.3f;
        private const float FAST_HEALTH_MULTIPLIER = 0.7f;
        private const float STRONG_DAMAGE_BONUS = 0.4f;
        private const float STRONG_MOVE_SPEED_PENALTY = 0.15f;
        private const float FAST_MOVE_SPEED_BONUS = 0.3f;

        public const int BALANCED = 0;
        public const int STRONG = 1;
        public const int FAST = 2;
        public const int COUNT = 3;

        public static bool IsValid(int characterId)
        {
            return characterId >= BALANCED && characterId < COUNT;
        }

        public static string GetName(int characterId)
        {
            return characterId switch
            {
                STRONG => "Силач",
                FAST => "Быстрый",
                _ => "Универсал"
            };
        }

        public static string GetDescription(int characterId)
        {
            return characterId switch
            {
                STRONG => "+40% урона\n+30% максимальных ОЗ\n−15% скорости",
                FAST => "+30% скорости\n−30% максимальных ОЗ",
                _ => "Сбалансированные характеристики.\nБез дополнительных модификаторов."
            };
        }

        public static void ApplyStartingStats(EntityManager manager, Entity playerEntity, Entity playerPrefab, int characterId)
        {
            CombatHealth health = manager.GetComponentData<CombatHealth>(playerPrefab);
            float healthMultiplier = characterId == STRONG ? STRONG_HEALTH_MULTIPLIER : characterId == FAST ? FAST_HEALTH_MULTIPLIER : 1f;
            health.Maximum = math.max(1, (int)math.round(health.Maximum * healthMultiplier));
            health.Current = health.Maximum;
            manager.SetComponentData(playerEntity, health);
            PlayerBonuses bonuses = manager.HasComponent<PlayerBonuses>(playerPrefab)
                ? manager.GetComponentData<PlayerBonuses>(playerPrefab) : default;
            bonuses.Damage += characterId == STRONG ? STRONG_DAMAGE_BONUS : 0f;
            bonuses.MoveSpeed += characterId == STRONG ? -STRONG_MOVE_SPEED_PENALTY : characterId == FAST ? FAST_MOVE_SPEED_BONUS : 0f;
            manager.SetComponentData(playerEntity, bonuses);
        }
    }
}
