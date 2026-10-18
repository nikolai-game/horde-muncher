using Unity.Entities;
using Unity.Mathematics;

namespace HordeMuncher
{
    public static class WaveMath
    {
        private const float TRAPEZOID_AREA_SCALE = 0.5f;

        public static float CalculateSpawnAmount(DynamicBuffer<WaveSpawnRateCurvePoint> spawnRatePoints,
            WavePlan wavePlan,
            float startTime,
            float endTime)
        {
            float curveIntervalDuration = wavePlan.Duration / (wavePlan.SpawnRatePointCount - 1);
            float spawnAmount = 0;
            startTime = math.clamp(startTime, 0, wavePlan.Duration);
            endTime = math.clamp(endTime, startTime, wavePlan.Duration);
            for (int curvePointIndex = 0; curvePointIndex < wavePlan.SpawnRatePointCount - 1; curvePointIndex++)
            {
                float intervalStart = math.max(startTime, curvePointIndex * curveIntervalDuration);
                float intervalEnd = math.min(endTime, (curvePointIndex + 1) * curveIntervalDuration);
                if (intervalEnd <= intervalStart)
                {
                    continue;
                }

                float startRate = spawnRatePoints[wavePlan.SpawnRatePointOffset + curvePointIndex].Rate;
                float endRate = spawnRatePoints[wavePlan.SpawnRatePointOffset + curvePointIndex + 1].Rate;
                spawnAmount += (math.lerp(startRate, endRate, (intervalStart - curvePointIndex * curveIntervalDuration) / curveIntervalDuration)
                    + math.lerp(startRate, endRate, (intervalEnd - curvePointIndex * curveIntervalDuration) / curveIntervalDuration))
                    * TRAPEZOID_AREA_SCALE
                    * (intervalEnd - intervalStart);
            }

            return spawnAmount;
        }
    }
}
