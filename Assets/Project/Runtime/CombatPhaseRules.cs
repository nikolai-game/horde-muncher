using Unity.Entities;

namespace HordeMuncher
{
    public static class CombatPhaseRules
    {
        public static bool IsActive(EntityManager entityManager)
        {
            using EntityQuery runs = entityManager.CreateEntityQuery(typeof(WaveProgress));
            if (runs.IsEmptyIgnoreFilter)
            {
                return true;
            }

            WaveProgress progress = runs.GetSingleton<WaveProgress>();
            return !progress.IsPaused && progress.Phase == WavePhase.Spawning;
        }
    }
}
