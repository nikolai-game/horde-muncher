using System.Linq;
using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Hud;
using HordeMuncher.UI.RunResult;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace HordeMuncher.Tests.PlayerStatus
{
    public sealed class RunResultPresenterTests
    {
        [TestCase(RunPhase.Victory, "Победа!")]
        [TestCase(RunPhase.Defeat, "Забег окончен")]
        public void FinishedRunDisplaysResultAndReadinessAcrossTheParty(RunPhase phase, string expectedTitle)
        {
            var model = new FakePlayerStatus();
            model.Result.Phase = phase;
            var view = new FakeRunResultView();
            using var presenter = new RunResultPresenter(new[]
            {
                model
            }, new[]
            {
                model
            }, view);
            presenter.Refresh(0);
            Assert.That(view.Data.IsVisible, Is.True);
            Assert.That(view.Data.Title, Is.EqualTo(expectedTitle));
            Assert.That(view.Data.Status, Does.Contain("1/3"));
            Assert.That(view.Data.RestartButtons[0].Text, Is.EqualTo("Ещё раз"));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EachOfFourLocalPlayersConfirmsOnlyItsOwnRestart(int localPlayerIndex)
        {
            var players = new FakePlayerStatus[4];
            for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
            {
                players[playerIndex] = new FakePlayerStatus();
                players[playerIndex].Result.PlayerNumber = playerIndex + 1;
                players[playerIndex].Result.Revision = (uint)(10 + playerIndex);
            }

            var view = new FakeRunResultView();
            using var presenter = new RunResultPresenter(players, players, view);
            presenter.Refresh(0);
            presenter.ToggleRestartReadiness(localPlayerIndex, 1);

            Assert.That(view.Data.RestartButtons.Length, Is.EqualTo(4));
            for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
            {
                Assert.That(players[playerIndex].CommandsSent, Is.EqualTo(playerIndex == localPlayerIndex ? 1 : 0));
            }

            Assert.That(players[localPlayerIndex].SentRevision, Is.EqualTo((uint)(10 + localPlayerIndex)));
        }

        [Test]
        public void SecondLocalPlayerSendsOnlyItsRevisionAndWaitsForServerConfirmation()
        {
            var first = new FakePlayerStatus();
            var second = new FakePlayerStatus();
            second.Result.PlayerNumber = 2;
            second.Result.Revision = 20;
            var view = new FakeRunResultView();
            using var presenter = new RunResultPresenter(new[]
            {
                first,
                second
            }, new[]
            {
                first,
                second
            }, view);
            presenter.Refresh(0);
            view.ClickRestart(1);
            view.ClickRestart(1);
            Assert.That(first.CommandsSent, Is.Zero);
            Assert.That(second.CommandsSent, Is.EqualTo(1));
            Assert.That(second.SentRevision, Is.EqualTo(20));
            Assert.That(second.Result.RestartReady, Is.False);
            Assert.That(view.Data.RestartButtons[0].IsEnabled, Is.True);
            Assert.That(view.Data.RestartButtons[1].IsEnabled, Is.False);
            presenter.Refresh(3);
            view.ClickRestart(1);
            Assert.That(second.CommandsSent, Is.EqualTo(1));
            Assert.That(view.Data.Status, Does.Contain("Ожидание ответа сервера"));
            second.Result.Revision++;
            second.Result.RestartReady = true;
            presenter.Refresh(4);
            Assert.That(view.Data.RestartButtons[1].IsEnabled, Is.True);
            Assert.That(view.Data.RestartButtons[1].Text, Is.EqualTo("Игрок 2 · Отменить готовность"));
            view.ClickRestart(1);
            Assert.That(second.CommandsSent, Is.EqualTo(2));
        }

        [Test]
        public void OtherPlayersReadinessDoesNotUnlockAnUnacknowledgedLocalCommand()
        {
            var model = new FakePlayerStatus();
            var view = new FakeRunResultView();
            using var presenter = new RunResultPresenter(new[]
            {
                model
            }, new[]
            {
                model
            }, view);
            presenter.Refresh(0);
            view.ClickRestart(0);
            model.Result.ReadyPlayers++;
            presenter.Refresh(1);
            Assert.That(view.Data.RestartButtons[0].IsEnabled, Is.False);
            Assert.That(view.Data.Status, Does.Contain("2/3"));
        }

        [Test]
        public void MissingPlayerCombatPhaseAndInvalidIndicesCannotSendCommands()
        {
            var model = new FakePlayerStatus();
            var view = new FakeRunResultView();
            using var presenter = new RunResultPresenter(new[]
            {
                model
            }, new[]
            {
                model
            }, view);
            model.Result.Phase = RunPhase.Combat;
            presenter.Refresh(0);
            Assert.That(view.Data.IsVisible, Is.False);
            presenter.ToggleRestartReadiness(0, 0);
            presenter.ToggleRestartReadiness(-1, 0);
            presenter.ToggleRestartReadiness(1, 0);
            model.Result.Phase = RunPhase.Victory;
            model.Available = false;
            presenter.Refresh(1);
            view.ClickRestart(0);
            Assert.That(view.Data.RestartButtons[0].IsVisible, Is.False);
            Assert.That(model.CommandsSent, Is.Zero);
        }

        [Test]
        public void RefusedCommandIsNotShownAsPendingAndDisposeUnsubscribes()
        {
            var model = new FakePlayerStatus
            {
                AcceptCommands = false
            };
            var view = new FakeRunResultView();
            using var presenter = new RunResultPresenter(new[]
            {
                model
            }, new[]
            {
                model
            }, view);
            presenter.Refresh(0);
            view.ClickRestart(0);
            Assert.That(view.Data.RestartButtons[0].IsEnabled, Is.True);
            Assert.That(view.SubscriberCount, Is.EqualTo(1));
            presenter.Dispose();
            Assert.That(view.SubscriberCount, Is.Zero);
            model.AcceptCommands = true;
            view.ClickRestart(0);
            presenter.ToggleRestartReadiness(0, 1);
            Assert.That(model.CommandsSent, Is.Zero);
        }

        [Test]
        public void LeavingResultAndStartingANewRunClearsPendingReadiness()
        {
            var model = new FakePlayerStatus();
            var view = new FakeRunResultView();
            using var presenter = new RunResultPresenter(new[]
            {
                model
            }, new[]
            {
                model
            }, view);
            presenter.Refresh(0);
            view.ClickRestart(0);
            model.Result.Phase = RunPhase.Combat;
            presenter.Refresh(1);
            Assert.That(view.Data.IsVisible, Is.False);
            model.Result.Phase = RunPhase.Defeat;
            presenter.Refresh(2);
            Assert.That(view.Data.RestartButtons[0].IsEnabled, Is.True);
        }

        [Test]
        public void PassiveViewDisplaysOnlyPresenterDataAndHidesUnusedLocalPlayerButton()
        {
            VisualElement root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/ArenaMenu.uxml").Instantiate();
            var model = new FakePlayerStatus();
            using var view = new RunResultView(root.Q("runResult"));
            using var presenter = new RunResultPresenter(new[]
            {
                model
            }, new[]
            {
                model
            }, view);
            presenter.Refresh(0);
            Assert.That(root.Q<Label>("runResultTitle").text, Is.EqualTo("Победа!"));
            Assert.That(root.Q<Button>("restartButton").enabledSelf, Is.True);
            Assert.That(root.Q<Button>("restartSecondButton").ClassListContains("hidden"), Is.True);
            presenter.ToggleRestartReadiness(0, 1);
            Assert.That(root.Q<Button>("restartButton").enabledSelf, Is.False);
            view.Dispose();
            Assert.That(root.Q("runResult").ClassListContains("hidden"), Is.True);
        }

        [Test]
        public void ResultPresenterAndContractsHaveNoUnitySimulationOrNetworkReferences()
        {
            System.Type[] types =
            {
                typeof(RunResultPresenter),
                typeof(IRunResultView)
            };
            foreach (System.Type type in types)
            {
                string[] references = type.Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
                Assert.That(references.Any(name => name.StartsWith("Unity")), Is.False);
                Assert.That(references, Does.Not.Contain("HordeMuncher.Runtime"));
            }
        }
    }
}
