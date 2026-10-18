using Unity.Mathematics;

namespace HordeMuncher
{
    public static class ArenaMovement
    {
        private const float MIN_OUTWARD_MOVEMENT = 0.0001f;
        private const float EQUAL_CORRECTION_WEIGHT = 0.5f;

        public static float3 ConstrainSharedViewMovement(float3 previousPosition,
            float3 proposedPosition,
            float3 partnerPreviousPosition,
            float3 partnerPosition,
            float maximumDistance)
        {
            float2 delta = proposedPosition.xz - partnerPosition.xz;
            float separationDistance = math.length(delta);
            if (separationDistance > maximumDistance)
            {
                float2 direction = delta / separationDistance;
                float outwardMovement = math.max(0, math.dot(proposedPosition.xz - previousPosition.xz, direction));
                float partnerOutwardMovement = math.max(0, math.dot(partnerPreviousPosition.xz - partnerPosition.xz, direction));
                float correctionWeight = outwardMovement + partnerOutwardMovement > MIN_OUTWARD_MOVEMENT
                    ? outwardMovement / (outwardMovement + partnerOutwardMovement)
                    : EQUAL_CORRECTION_WEIGHT;
                proposedPosition.xz -= direction * ((separationDistance - maximumDistance) * correctionWeight);
            }

            return proposedPosition;
        }

        public static float ClampReportedViewDistance(float reportedDistance, float maximumDistance) =>
            math.isfinite(reportedDistance) && reportedDistance > 0
            ? math.clamp(reportedDistance, ArenaLimits.MIN_SHARED_VIEW_DISTANCE, maximumDistance)
            : maximumDistance;

        public static float3 CalculateNextPosition(float3 position, float2 input, float speed, float arenaHalfExtent, float deltaTime)
        {
            if (!math.all(math.isfinite(input)))
            {
                input = float2.zero;
            }

            input /= math.max(1f, math.length(input));
            position += new float3(input.x, 0, input.y) * math.max(0, speed) * deltaTime;
            position.xz = math.clamp(position.xz, new float2(-arenaHalfExtent), new float2(arenaHalfExtent));
            return position;
        }
    }
}
