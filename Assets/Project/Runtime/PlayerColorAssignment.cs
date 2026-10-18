using Unity.Mathematics;

namespace HordeMuncher
{
    public static class PlayerColorAssignment
    {
        public const int COLOR_COUNT = 4;

        public static bool TryAssign(int usedColors, ref Random random, out int colorIndex)
        {
            int availableCount = COLOR_COUNT - math.countbits((uint)(usedColors & 15));
            colorIndex = -1;
            if (availableCount == 0)
            {
                return false;
            }

            int selectedAvailableColor = random.NextInt(availableCount);
            for (int candidate = 0; candidate < COLOR_COUNT; candidate++)
            {
                if ((usedColors & (1 << candidate)) != 0)
                {
                    continue;
                }

                if (selectedAvailableColor == 0)
                {
                    colorIndex = candidate;
                    return true;
                }

                selectedAvailableColor--;
            }

            return false;
        }
    }
}
