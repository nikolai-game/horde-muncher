using System;
using HordeMuncher.UI.Contracts;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Shop.View
{
    public sealed class ShopViewButton
    {
        public string SelectionKey;
        public Button Button;
        public Action Invoke;
        public ShopButtonData CommandData;
        public ShopCardData WeaponCard;
        public VisualElement ItemTarget;
        public VisualElement ItemActions;
    }
}
