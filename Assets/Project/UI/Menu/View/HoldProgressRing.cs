using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Menu
{
    public sealed class HoldProgressRing : VisualElement
    {
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
            if (size <= 6)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            Vector2 center = contentRect.center;
            float radius = (size - 6) * 0.5f;
            painter.lineWidth = 5;
            painter.strokeColor = new Color(1, 1, 1, 0.18f);
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
            painter.Stroke();
            if (_progress <= 0)
            {
                return;
            }

            painter.strokeColor = _progressColor;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(-90), Angle.Degrees(-90 + 360 * _progress));
            painter.Stroke();
        }
    }
}
