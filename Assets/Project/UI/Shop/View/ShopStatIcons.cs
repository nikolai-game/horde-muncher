using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Shop.View
{
    public static class ShopStatIcons
    {
        private const int GRID_SIZE = 4;
        private static readonly Texture2D _atlas = Resources.Load<Texture2D>("StatIcons");

        public static Image Create(int cellIndex)
        {
            var icon = new Image
            {
                image = _atlas,
                uv = new Rect(cellIndex % GRID_SIZE / (float)GRID_SIZE,
                    (GRID_SIZE - 1 - cellIndex / GRID_SIZE) / (float)GRID_SIZE, 1f / GRID_SIZE, 1f / GRID_SIZE),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("shop-stat-icon");
            return icon;
        }
    }
}
