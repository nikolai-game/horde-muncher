using Unity.Mathematics;

namespace HordeMuncher
{
    public static class PlayerColorAssignment
    {
        private const int ALL_COLORS_MASK = (1 << COLOR_COUNT) - 1;

        public const int COLOR_COUNT = ArenaLimits.MAX_PLAYERS;

        public static bool TryAssign(int usedColors, ref Random random, out int colorIndex)
        {
            int availableCount = COLOR_COUNT - math.countbits((uint)(usedColors & ALL_COLORS_MASK));
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
