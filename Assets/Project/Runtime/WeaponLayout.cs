using Unity.Mathematics;

namespace HordeMuncher
{
    public static class WeaponLayout
    {
        public static float3 CalculateMountPosition(float3 playerPosition, int weaponSlot, int weaponSlotCount, float orbitRadius, float mountHeight)
        {
            float angle = math.PI * 2 * weaponSlot / math.max(1, weaponSlotCount);
            return playerPosition + new float3(math.cos(angle) * orbitRadius, mountHeight, math.sin(angle) * orbitRadius);
        }
    }
}
