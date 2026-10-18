using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Hud;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace HordeMuncher.Tests.PlayerStatus
{
    public sealed class HudPresenterTests
    {
        [Test]
        public void HudDisplaysPlayerHealthProgressResourcesAndWaveFromPlainState()
        {
            var model = new FakePlayerStatus();
            VisualElement root = CreateHud();
            using var presenter = new HudPresenter(new[]
            {
                model
            }, root, CreatePlayerPanel);
            presenter.Refresh();
            Assert.That(root.Q<Label>("playerName"), Is.Null);
            Assert.That(root.Q("materialBag").ClassListContains("hidden"), Is.True);
            Assert.That(root.Q<Label>("bankedResourcesText").text, Is.EqualTo("0"));
            Assert.That(root.Q<Label>("healthText").text, Is.EqualTo("80 / 100"));
            Assert.That(root.Q("healthFill").style.width.value.value, Is.EqualTo(80));
            Assert.That(root.Q<Label>("experienceText").text, Is.EqualTo("LV.2"));
            Assert.That(root.Q("experienceFill").style.width.value.value, Is.EqualTo(30).Within(0.01));
            Assert.That(root.Q<Label>("resourcesText").text, Is.EqualTo("12"));
            Assert.That(root.Q<Label>("waveLabel").text, Is.EqualTo("ВОЛНА 3"));
            Assert.That(root.Q<Label>("waveTimer").text, Is.EqualTo("10"));
        }

        [TestCase(RunPhase.Waiting, "Ожидание игроков")]
        [TestCase(RunPhase.WaveCleared, "")]
        [TestCase(RunPhase.Shop, "")]
        [TestCase(RunPhase.Victory, "Все волны пройдены")]
        [TestCase(RunPhase.Defeat, "Забег окончен")]
        public void NonCombatPhasesClearTimerAndDisplayTheirMeaning(RunPhase phase, string expected)
        {
            var model = new FakePlayerStatus();
            VisualElement root = CreateHud();
            using var presenter = new HudPresenter(new[]
            {
                model
            }, root, CreatePlayerPanel);
            presenter.Refresh();
            model.Hud.Phase = phase;
            presenter.Refresh();
            Assert.That(root.Q<Label>("waveTimer").text, Is.EqualTo(phase == RunPhase.WaveCleared ? "0" : ""));
            Assert.That(root.Q<Label>("wavePhase").text, Is.EqualTo(expected));
            Assert.That(root.ClassListContains("hidden"), Is.EqualTo(phase == RunPhase.Shop));
            model.Hud.Phase = RunPhase.Combat;
            presenter.Refresh();
            Assert.That(root.ClassListContains("hidden"), Is.False);
        }

        [Test]
        public void DeadPlayerAndInvalidProgressDenominatorsKeepBarsWithinBounds()
        {
            var model = new FakePlayerStatus();
            model.Hud.Health = -1;
            model.Hud.MaximumHealth = 0;
            model.Hud.Experience = 15;
            model.Hud.NextLevelExperience = 0;
            VisualElement root = CreateHud();
            using var presenter = new HudPresenter(new[]
            {
                model
            }, root, CreatePlayerPanel);
            presenter.Refresh();
            Assert.That(root.Q<Label>("healthText").text, Is.EqualTo("Погиб"));
            Assert.That(root.Q("healthFill").style.width.value.value, Is.Zero);
            Assert.That(root.Q("experienceFill").style.width.value.value, Is.EqualTo(100));
        }

        [Test]
        public void TwoLocalPlayersHideIndependentlyAndDisposeRemovesHud()
        {
            var first = new FakePlayerStatus();
            var second = new FakePlayerStatus();
            second.Hud.PlayerNumber = 2;
            second.Hud.BankedResources = 5;
            second.Hud.Health = 20;
            VisualElement root = CreateHud();
            var parent = new VisualElement();
            parent.Add(root);
            using var presenter = new HudPresenter(new[]
            {
                first,
                second
            }, root, CreatePlayerPanel);
            presenter.Refresh();
            VisualElement panels = root.Q("hudPlayers");
            Assert.That(panels.childCount, Is.EqualTo(2));
            Assert.That(panels[0].Q("materialBag").ClassListContains("hidden"), Is.True);
            Assert.That(panels[1].Q("materialBag").ClassListContains("hidden"), Is.False);
            Assert.That(panels[1].Q<Label>("healthText").text, Is.EqualTo("20 / 100"));
            first.Available = false;
            presenter.Refresh();
            Assert.That(panels[0].ClassListContains("hidden"), Is.True);
            Assert.That(root.ClassListContains("hidden"), Is.False);
            second.Available = false;
            presenter.Refresh();
            Assert.That(root.ClassListContains("hidden"), Is.True);
            presenter.Dispose();
            Assert.That(parent.childCount, Is.Zero);
        }

        [Test]
        public void CompletionLettersAppearOverTwoSecondsAndBagDisplaysReserve()
        {
            var model = new FakePlayerStatus();
            model.Hud.Phase = RunPhase.WaveCleared;
            model.Hud.BankedResources = 7;
            VisualElement root = CreateHud();
            using var presenter = new HudPresenter(new[] { model }, root, CreatePlayerPanel);
            presenter.Refresh();
            VisualElement completion = root.Q("waveCompletion");
            Assert.That(completion.ClassListContains("hidden"), Is.False);
            Assert.That(completion[0].style.opacity.value, Is.Zero);
            Assert.That(root.Q<Label>("bankedResourcesText").text, Is.EqualTo("7"));
            Assert.That(root.Q("materialBag").ClassListContains("hidden"), Is.False);
            presenter.RefreshWaveCompletionAnimation(1);
            Assert.That(completion[0].style.opacity.value, Is.EqualTo(1));
            Assert.That(completion[completion.childCount - 1].style.opacity.value, Is.Zero);
            presenter.RefreshWaveCompletionAnimation(1);
            Assert.That(completion[completion.childCount - 1].style.opacity.value, Is.EqualTo(1));
            model.Hud.Phase = RunPhase.Combat;
            model.Hud.BankedResources = 0;
            presenter.Refresh();
            Assert.That(completion.ClassListContains("hidden"), Is.True);
            Assert.That(root.Q("materialBag").ClassListContains("hidden"), Is.True);
            model.Hud.BankedResources = 3;
            presenter.Refresh();
            Assert.That(root.Q("materialBag").ClassListContains("hidden"), Is.False);
        }

        [Test]
        public void CompletionAnimationAdvancesBetweenHudRefreshesAndRestartsForNextWave()
        {
            var model = new FakePlayerStatus();
            model.Hud.Phase = RunPhase.WaveCleared;
            VisualElement root = CreateHud();
            using var presenter = new HudPresenter(new[] { model }, root, CreatePlayerPanel);
            presenter.Refresh();
            VisualElement firstLetter = root.Q("waveCompletion")[0];
            presenter.RefreshWaveCompletionAnimation(1f / 60);
            float firstFrameOpacity = firstLetter.style.opacity.value;
            Assert.That(firstFrameOpacity, Is.GreaterThan(0).And.LessThan(1));
            presenter.Refresh();
            Assert.That(firstLetter.style.opacity.value, Is.EqualTo(firstFrameOpacity));
            presenter.RefreshWaveCompletionAnimation(1f / 60);
            Assert.That(firstLetter.style.opacity.value, Is.GreaterThan(firstFrameOpacity));

            model.Hud.IsPaused = true;
            presenter.Refresh();
            float pausedOpacity = firstLetter.style.opacity.value;
            presenter.RefreshWaveCompletionAnimation(1);
            Assert.That(firstLetter.style.opacity.value, Is.EqualTo(pausedOpacity));
            model.Hud.IsPaused = false;
            presenter.Refresh();
            presenter.RefreshWaveCompletionAnimation(0.25f);
            Assert.That(firstLetter.style.opacity.value, Is.EqualTo(1));

            model.Hud.Phase = RunPhase.Combat;
            presenter.Refresh();
            presenter.RefreshWaveCompletionAnimation(1);
            model.Hud.Phase = RunPhase.WaveCleared;
            presenter.Refresh();
            Assert.That(firstLetter.style.opacity.value, Is.Zero);
        }

        [Test]
        public void RewardIndicatorsDisplayCollectedItemsAndPendingLevelsIndependently()
        {
            var model = new FakePlayerStatus();
            model.Hud.CollectedItems = 3;
            model.Hud.PendingUpgrades = 2;
            VisualElement root = CreateHud();
            using var presenter = new HudPresenter(new[]
            {
                model
            }, root, CreatePlayerPanel);
            presenter.Refresh();
            Assert.That(root.Q("collectedItems").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q<Label>("collectedItemsText").text, Is.EqualTo("3"));
            Assert.That(root.Q<Label>("pendingUpgradesText").text, Is.EqualTo("2"));
            model.Hud.PendingUpgrades = 0;
            presenter.Refresh();
            Assert.That(root.Q("pendingUpgrades").ClassListContains("hidden"), Is.True);
            Assert.That(root.Q("collectedItems").ClassListContains("hidden"), Is.False);
            model.Hud.CollectedItems = 0;
            presenter.Refresh();
            Assert.That(root.Q("collectedItems").ClassListContains("hidden"), Is.True);
        }

        private static VisualElement CreateHud()
        {
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/CombatHud.uxml").Instantiate();
        }

        private static VisualElement CreatePlayerPanel()
        {
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/PlayerHud.uxml").Instantiate();
        }
    }
}
