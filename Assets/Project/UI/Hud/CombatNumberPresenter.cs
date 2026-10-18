using System;
using System.Collections.Generic;
using HordeMuncher.UI.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Hud
{
    public sealed class CombatNumberPresenter : IDisposable
    {
        private const float LIFETIME = 0.85f;
        private const int MAX_VISIBLE_NUMBERS = 128;

        private readonly ICombatNumberSource _source;
        private readonly VisualElement _layer;
        private readonly List<CombatNumberAnimation> _animations = new List<CombatNumberAnimation>();
        private readonly Stack<Label> _availableLabels = new Stack<Label>();
        private int _numberSequence;

        public CombatNumberPresenter(ICombatNumberSource source, VisualElement root)
        {
            _source = source;
            _layer = new VisualElement { pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("combat-number-layer");
            root.Add(_layer);
        }

        public void Refresh(float now)
        {
            while (_source.TryRead(out CombatNumberState state))
            {
                if (state.HealthChange == 0 && !state.IsResourceBonus)
                {
                    continue;
                }

                if (_animations.Count >= MAX_VISIBLE_NUMBERS)
                {
                    RemoveNumber(0);
                }

                Label label = _availableLabels.Count > 0 ? _availableLabels.Pop() : new Label();
                label.pickingMode = PickingMode.Ignore;
                label.AddToClassList("combat-number");
                label.EnableInClassList("combat-number-critical", !state.IsPlayer && state.HealthChange < 0 && state.IsCritical);
                label.EnableInClassList("combat-number-player-damage", state.IsPlayer && state.HealthChange < 0);
                label.EnableInClassList("combat-number-healing", state.HealthChange > 0);
                label.EnableInClassList("combat-number-resource-bonus", state.IsResourceBonus);
                label.text = state.IsResourceBonus ? "x2"
                    : state.HealthChange > 0 ? "+" + state.HealthChange : (-(long)state.HealthChange).ToString();
                _layer.Add(label);
                _animations.Add(new CombatNumberAnimation
                {
                    State = state,
                    Label = label,
                    StartedAt = now,
                    HorizontalOffset = (_numberSequence++ % 3 - 1) * 18
                });
            }

            for (int index = _animations.Count - 1; index >= 0; index--)
            {
                CombatNumberAnimation animation = _animations[index];
                float progress = (now - animation.StartedAt) / LIFETIME;
                if (progress >= 1)
                {
                    RemoveNumber(index);
                    continue;
                }

                bool visible = _source.TryGetPanelPosition(animation.State, out float panelX, out float panelY);
                animation.Label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                animation.Label.style.left = panelX - 50 + animation.HorizontalOffset;
                animation.Label.style.top = panelY - 32 - progress * 55;
                animation.Label.style.opacity = 1 - Mathf.Clamp01((progress - 0.55f) / 0.45f);
            }
        }

        public void Dispose()
        {
            _animations.Clear();
            _availableLabels.Clear();
            _layer.RemoveFromHierarchy();
        }

        private void RemoveNumber(int index)
        {
            Label label = _animations[index].Label;
            label.RemoveFromHierarchy();
            _availableLabels.Push(label);
            _animations.RemoveAt(index);
        }
    }
}
