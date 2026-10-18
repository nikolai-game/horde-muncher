using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Menu
{
    public sealed class HoldProgressRing : VisualElement
    {
        private const int RING_PADDING = 6;
        private const float RADIUS_FROM_DIAMETER = 0.5f;
        private const int RING_LINE_WIDTH = 5;
        private const float BACKGROUND_OPACITY = 0.18f;
        private const int FULL_CIRCLE_DEGREES = 360;
        private const int START_ANGLE_DEGREES = 90;

        private float _progress;
        private Color _progressColor = Color.white;

        public HoldProgressRing()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("lobby-hold-ring");
            generateVisualContent += GenerateVisualContentHandler;
        }

        public void Render(float progress, Color progressColor)
        {
            _progress = Mathf.Clamp01(progress);
            _progressColor = progressColor;
            MarkDirtyRepaint();
        }

        private void GenerateVisualContentHandler(MeshGenerationContext context)
        {
            float size = Mathf.Min(contentRect.width, contentRect.height);
            if (size <= RING_PADDING)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            Vector2 center = contentRect.center;
            float radius = (size - RING_PADDING) * RADIUS_FROM_DIAMETER;
            painter.lineWidth = RING_LINE_WIDTH;
            painter.strokeColor = new Color(1, 1, 1, BACKGROUND_OPACITY);
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(FULL_CIRCLE_DEGREES));
            painter.Stroke();
            if (_progress <= 0)
            {
                return;
            }

            painter.strokeColor = _progressColor;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(-START_ANGLE_DEGREES), Angle.Degrees(-START_ANGLE_DEGREES + FULL_CIRCLE_DEGREES * _progress));
            painter.Stroke();
        }
    }
}
