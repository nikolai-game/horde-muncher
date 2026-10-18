using System.Collections.Generic;
using System.Reflection;
using HordeMuncher.Client.Menu;
using HordeMuncher.UI.Hud;
using HordeMuncher.UI.Menu;
using NUnit.Framework;
using UnityEditor;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.Tests.Menu
{
    public sealed class ArenaMenuPresenterTests
    {
        [Test]
        public void MenuUsesConfiguredConnectionAndUnsubscribesOnDispose()
        {
            var session = new FakeArenaMenuSession();
            var view = new FakeArenaMenuView();
            using var presenter = new ArenaMenuPresenter(session, session, view, "192.168.1.5", "8000");
            view.ClickHostSession();
            view.ClickJoinServer();
            view.ClickResumeGame();
            view.ClickExitToMenu();
            view.ClickQuitGame();
            Assert.That(session.Commands, Is.EqualTo(new[]
            {
                "Host 192.168.1.5:8000",
                "Join room ABC123",
                "Resume",
                "Stop",
                "Quit"
            }));
            presenter.Dispose();
            view.ClickHostSession();
            view.ClickJoinServer();
            view.ClickResumeGame();
            view.ClickExitToMenu();
            view.ClickQuitGame();
            Assert.That(session.Commands.Count, Is.EqualTo(5));
        }

        [Test]
        public void SettingsCloseOnBackAndWhenSessionStarts()
        {
            var session = new FakeArenaMenuSession();
            var view = new FakeArenaMenuView();
            using var presenter = new ArenaMenuPresenter(session, session, view, "192.168.1.5", "8000");
            view.ClickOpenSettings();
            Assert.That(view.IsSettingsVisible, Is.True);
            view.ClickCloseSettings();
            Assert.That(view.IsSettingsVisible, Is.False);
            view.ClickOpenSettings();
            Assert.That(presenter.CloseSettings(), Is.True);
            Assert.That(view.IsSettingsVisible, Is.False);
            Assert.That(presenter.CloseSettings(), Is.False);
            view.ClickOpenSettings();
            session.State.IsRunning = true;
            presenter.Refresh();
            Assert.That(view.IsSettingsVisible, Is.False);
            Assert.That(session.Commands, Is.Empty);
        }

        [Test]
        public void SessionFailureIsDisplayedAndDisconnectReturnsFocusToTheMenuOnce()
        {
            var session = new FakeArenaMenuSession();
            var view = new FakeArenaMenuView();
            using var presenter = new ArenaMenuPresenter(session, session, view, "192.168.1.5", "8000");
            session.State.Status = "Порт занят";
            view.ClickHostSession();
            Assert.That(view.State.IsRunning, Is.False);
            Assert.That(view.State.Status, Is.EqualTo("Порт занят"));
            session.State.IsRunning = true;
            presenter.Refresh();
            Assert.That(view.State.IsRunning, Is.True);
            session.State.IsRunning = false;
            session.State.Status = "Соединение потеряно";
            presenter.Refresh();
            presenter.Refresh();
            Assert.That(view.State.Status, Is.EqualTo("Соединение потеряно"));
            Assert.That(view.HostFocusCount, Is.EqualTo(2));
        }

        [Test]
        public void EscapeDuringRunningGameDoesNotExitToMenu()
        {
            var sessionObject = new GameObject("Pause input test");
            sessionObject.SetActive(false);
            try
            {
                ArenaMenu menu = sessionObject.AddComponent<ArenaMenu>();
                ArenaSession session = sessionObject.GetComponent<ArenaSession>();
                var clientWorld = new World("Pause input client");
                ((List<World>)typeof(ArenaSession).GetField("_sessionWorlds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(session)).Add(clientWorld);
                ((List<World>)typeof(ArenaSession).GetField("_clientWorlds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(session)).Add(clientWorld);
                Entity lobby = clientWorld.EntityManager.CreateEntity(typeof(LobbyClientSnapshot));
                clientWorld.EntityManager.SetComponentData(lobby, new LobbyClientSnapshot { Started = true });
                VisualElement root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/ArenaMenu.uxml").Instantiate();
                typeof(ArenaMenu).GetField("_documentRoot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, root);
                typeof(ArenaMenu).GetField("_sessionGateway", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(menu, new ArenaSessionGateway(session));

                using NavigationCancelEvent cancel = NavigationCancelEvent.GetPooled();
                typeof(ArenaMenu).GetMethod("NavigationCancelHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(menu, new object[] { cancel });
                Assert.That(session.IsRunning, Is.True);
                Assert.That(cancel.isPropagationStopped, Is.True);
            }
            finally
            {
                sessionObject.GetComponent<ArenaSession>()?.StopSession();
                Object.DestroyImmediate(sessionObject);
            }
        }

        [Test]
        public void PauseScreenFollowsSessionStateAndStaysAboveOtherScreens()
        {
            VisualElement root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/ArenaMenu.uxml").Instantiate();
            var session = new FakeArenaMenuSession();
            using var view = new ArenaMenuView(root);
            using var presenter = new ArenaMenuPresenter(session, session, view, "127.0.0.1", "7979");
            VisualElement pauseScreen = root.Q("pauseScreen");
            Assert.That(pauseScreen.ClassListContains("hidden"), Is.True);

            root.Add(new VisualElement { name = "shopOverlay" });
            session.State.IsRunning = true;
            session.State.IsPaused = true;
            presenter.Refresh();
            Assert.That(pauseScreen.ClassListContains("hidden"), Is.False);
            Assert.That(root.IndexOf(pauseScreen), Is.EqualTo(root.childCount - 1));
            Assert.That(root.Q<Button>("pauseExitToMenuButton").text, Is.EqualTo("В меню"));
            Assert.That(root.Q<Button>("pauseQuitGameButton").text, Is.EqualTo("Выход из игры"));
            Assert.That(root.Q("sessionHud").ClassListContains("hidden"), Is.True);
            Assert.That(session.Commands, Is.Empty);

            session.State.IsPaused = false;
            presenter.Refresh();
            Assert.That(pauseScreen.ClassListContains("hidden"), Is.True);

            session.State.IsPaused = true;
            session.State.IsRunning = false;
            presenter.Refresh();
            Assert.That(pauseScreen.ClassListContains("hidden"), Is.True);
            Assert.That(root.Q("startScreen").ClassListContains("hidden"), Is.False);
        }

        [Test]
        public void PassiveMenuAndIndependentSeparationWarningRenderTheirOwnState()
        {
            VisualElement root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/ArenaMenu.uxml").Instantiate();
            var session = new FakeArenaMenuSession();
            using var view = new ArenaMenuView(root);
            using var menu = new ArenaMenuPresenter(session, session, view, "192.168.1.5", "8000");
            VisualElement warningElement = root.Q("sharedViewWarning");
            using var warning = new PlayerSeparationWarningPresenter(session, warningElement);
            Assert.That(root.Q("startScreen").ClassListContains("hidden"), Is.False);
            Assert.That(warningElement.ClassListContains("hidden"), Is.True);
            session.State.IsRunning = true;
            session.State.Status = "Подключение к арене…";
            session.IsWarningVisible = true;
            menu.Refresh();
            warning.Refresh();
            Assert.That(root.Q("startScreen").ClassListContains("hidden"), Is.True);
            Assert.That(root.Q("sessionHud").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q<Label>("sessionStatus").text, Is.EqualTo("Подключение к арене…"));
            Assert.That(root.Q("addressField"), Is.Null);
            Assert.That(root.Q("portField"), Is.Null);
            Assert.That(root.Q("localButton"), Is.Null);
            Assert.That(root.Q<Button>("quitGameButton").text, Is.EqualTo("Выход из игры"));
            Assert.That(warningElement.ClassListContains("hidden"), Is.False);
            warning.Dispose();
            Assert.That(warningElement.ClassListContains("hidden"), Is.True);
        }
    }
}
