using System;
using UnityEngine;

namespace HordeMuncher
{
    [Serializable]
    public sealed class ItemData
    {
        public string Name;
        [Tooltip("Иконка магазина и инвентаря. Если не задана, показываются первые буквы названия.")]
        public Sprite Icon;
        [TextArea]
        public string Description;
        public BonusKind Bonus;
        public float Amount;
        public BonusKind Penalty;
        public float PenaltyAmount;
        [Min(0)]
        public int Price = 8;
        [Min(0.01f)]
        public float Weight = 1;
    }
}
