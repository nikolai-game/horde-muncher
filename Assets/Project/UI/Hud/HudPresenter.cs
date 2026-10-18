using System;
using System.Collections.Generic;
using HordeMuncher.UI.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Hud
{
    public sealed class HudPresenter : IDisposable
    {
        private const int FALLBACK_COMPLETION_FONT_SIZE = 72;
        private const float COMPLETION_FONT_WIDTH_FRACTION = 0.07f;
        private const int MIN_COMPLETION_FONT_SIZE = 28;
        private const int MAX_COMPLETION_FONT_SIZE = 96;
        private const float COMPLETION_STAGGER_SECONDS = 1.75f;
        private const float LETTER_ANIMATION_SECONDS = 0.25f;
        private const int LETTER_START_OFFSET = 24;
        private const float LETTER_START_SCALE = 1.35f;
        private const float LETTER_SCALE_OVERSHOOT = 0.35f;

        private const string WAVE_COMPLETION_TEXT = "WAVE COMPLETED!";

        private readonly Label[] _completionLetters = new Label[WAVE_COMPLETION_TEXT.Length];
        private readonly IReadOnlyList<IHudStateSource> _playerStateSources;
        private readonly VisualElement _root;
        private readonly VisualElement[] _playerPanels;
        private bool _waveCompletionVisible;
        private bool _waveCompletionPaused;
        private float _waveCompletionElapsed;

        public HudPresenter(IReadOnlyList<IHudStateSource> playerStateSources, VisualElement root, Func<VisualElement> createPlayerPanel)
        {
            _playerStateSources = playerStateSources;
            _root = root;
            _playerPanels = new VisualElement[playerStateSources.Count];
            VisualElement container = root.Q("hudPlayers");
            for (int playerIndex = 0; playerIndex < playerStateSources.Count; playerIndex++)
            {
                _playerPanels[playerIndex] = createPlayerPanel();
                _playerPanels[playerIndex].AddToClassList("hud-player-slot");
                container.Add(_playerPanels[playerIndex]);
            }

            VisualElement completion = root.Q("waveCompletion");
            for (int letterIndex = 0; letterIndex < _completionLetters.Length; letterIndex++)
            {
                var letter = new Label(WAVE_COMPLETION_TEXT[letterIndex].ToString()) { pickingMode = PickingMode.Ignore };
                letter.AddToClassList("wave-completion-letter");
                _completionLetters[letterIndex] = letter;
                completion.Add(letter);
            }

            _root.EnableInClassList("hidden", true);
        }

        public void Refresh()
        {
            bool hasVisiblePlayer = false;
            bool waveCompleted = false;
            for (int playerIndex = 0; playerIndex < _playerStateSources.Count; playerIndex++)
            {
                VisualElement panel = _playerPanels[playerIndex];
                bool available = _playerStateSources[playerIndex].TryRead(out HudState state);
                panel.EnableInClassList("hidden", !available || state.Phase == RunPhase.Shop);
                if (!available)
                {
                    continue;
                }

                VisualElement playerPanel = panel.Q("playerHud") ?? panel;
                for (int colorIndex = 0; colorIndex < ArenaLimits.MAX_PLAYERS; colorIndex++)
                {
                    playerPanel.EnableInClassList("player-color-" + colorIndex, colorIndex == (state.PlayerNumber - 1) % ArenaLimits.MAX_PLAYERS);
                    panel.EnableInClassList("hud-corner-" + colorIndex, colorIndex == (state.PlayerNumber - 1) % ArenaLimits.MAX_PLAYERS);
                }

                playerPanel.EnableInClassList("player-local", state.IsLocal);
                panel.Q<Label>("healthText").text = state.Health <= 0 ? "Погиб" : state.Health + " / " + state.MaximumHealth;
                panel.Q("healthFill").style.width = CalculateFillPercent(state.Health, state.MaximumHealth);
                RefreshRewardIndicator(panel, "pendingUpgrades", "pendingUpgradesText", state.PendingUpgrades);
                RefreshRewardIndicator(panel, "collectedItems", "collectedItemsText", state.CollectedItems);
                panel.Q<Label>("experienceText").text = "LV." + state.Level;
                panel.Q("experienceFill").style.width = CalculateFillPercent(state.Experience, state.NextLevelExperience);
                panel.Q<Label>("resourcesText").text = state.Resources.ToString();
                panel.Q<Label>("bankedResourcesText").text = state.BankedResources.ToString();
                panel.Q("materialBag").EnableInClassList("hidden", state.BankedResources <= 0);
                if (!hasVisiblePlayer)
                {
                    waveCompleted = state.Phase == RunPhase.WaveCleared;
                    if (waveCompleted && !_waveCompletionVisible)
                    {
                        _waveCompletionElapsed = state.WaveCompletionElapsed;
                    }

                    _waveCompletionPaused = state.IsPaused;
                    _root.Q<Label>("waveLabel").text = "ВОЛНА " + state.WaveNumber;
                    _root.Q<Label>("waveTimer").text = waveCompleted ? "0"
                        : state.Phase == RunPhase.Combat ? Mathf.CeilToInt(state.WaveSecondsRemaining).ToString() : "";
                    _root.Q<Label>("wavePhase").text = state.IsPaused ? "Пауза · Esc / Start — продолжить" : FormatPhaseText(state.Phase);
                }

                hasVisiblePlayer |= state.IsPaused || state.Phase != RunPhase.Shop;
            }

            _waveCompletionVisible = hasVisiblePlayer && waveCompleted;
            RefreshWaveCompletionAnimation(0);
            _root.EnableInClassList("wave-completed", _waveCompletionVisible);
            _root.Q("waveCompletion").EnableInClassList("hidden", !hasVisiblePlayer || !waveCompleted);
            _root.EnableInClassList("hidden", !hasVisiblePlayer);
        }

        public void Dispose()
        {
            _root.RemoveFromHierarchy();
        }

        public void RefreshWaveCompletionAnimation(float unscaledDeltaTime)
        {
            if (!_waveCompletionVisible)
            {
                return;
            }

            if (!_waveCompletionPaused)
            {
                _waveCompletionElapsed += unscaledDeltaTime;
            }

            RefreshWaveCompletionLetters();
        }

        private static void RefreshRewardIndicator(VisualElement panel, string indicatorName, string countName, int count)
        {
            VisualElement indicator = panel.Q(indicatorName);
            if (indicator == null)
            {
                return;
            }

            indicator.EnableInClassList("hidden", count <= 0);
            indicator.Q<Label>(countName).text = count > 1 ? count.ToString() : "";
        }

        private void RefreshWaveCompletionLetters()
        {
            float width = _root.resolvedStyle.width;
            float fontSize = float.IsNaN(width) ? FALLBACK_COMPLETION_FONT_SIZE : Mathf.Clamp(width * COMPLETION_FONT_WIDTH_FRACTION,
                MIN_COMPLETION_FONT_SIZE, MAX_COMPLETION_FONT_SIZE);
            for (int letterIndex = 0; letterIndex < _completionLetters.Length; letterIndex++)
            {
                float delay = letterIndex * COMPLETION_STAGGER_SECONDS / (_completionLetters.Length - 1);
                float progress = Mathf.Clamp01((_waveCompletionElapsed - delay) / LETTER_ANIMATION_SECONDS);
                Label letter = _completionLetters[letterIndex];
                letter.style.fontSize = fontSize;
                letter.style.opacity = progress;
                letter.style.translate = new Translate(0, (1 - progress) * LETTER_START_OFFSET);
                letter.style.scale = new Scale(Vector3.one * (LETTER_START_SCALE - progress * LETTER_SCALE_OVERSHOOT));
            }
        }

        private static Length CalculateFillPercent(int currentAmount, int maximum)
        {
            return Length.Percent(StatUnits.PERCENT_SCALE * Mathf.Clamp01(currentAmount / (float)Mathf.Max(1, maximum)));
        }

        private static string FormatPhaseText(RunPhase phase)
        {
            return phase switch
            {
                RunPhase.Waiting => "Ожидание игроков",
                RunPhase.WaveCleared => "",
                RunPhase.Rewards => "Выбор наград",
                RunPhase.Victory => "Все волны пройдены",
                RunPhase.Defeat => "Забег окончен",
                _ => ""
            };
        }
    }
}
