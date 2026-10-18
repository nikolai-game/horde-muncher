using UnityEngine;

namespace HordeMuncher
{
    [CreateAssetMenu(menuName = "Horde Muncher/Player Color Settings")]
    public sealed class PlayerColorSettings : ScriptableObject
    {
        private const int THIRD_COLOR_INDEX = 2;
        private const int FOURTH_COLOR_INDEX = 3;

        public Color FirstColor = new Color(0.1f, 0.75f, 1f);
        public Color SecondColor = new Color(1f, 0.25f, 0.15f);
        public Color ThirdColor = new Color(0.3f, 1f, 0.2f);
        public Color FourthColor = new Color(0.85f, 0.25f, 1f);
        [Min(0.1f)]
        public float GlowRadius = 0.9f;
        public float GroundHeight = 0.03f;
        [Min(0)]
        public float GlowIntensity = 1.5f;
        public Material GlowMaterial;

        public Color GetColor(int colorIndex)
        {
            switch (colorIndex)
            {
                case 1: return SecondColor;
                case THIRD_COLOR_INDEX: return ThirdColor;
                case FOURTH_COLOR_INDEX: return FourthColor;
                default: return FirstColor;
            }
        }
    }
}
