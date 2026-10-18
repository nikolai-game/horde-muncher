using HordeMuncher.UI.Contracts;
using HordeMuncher.UI.Menu;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace HordeMuncher.Tests.Menu
{
    public sealed class LobbyLayoutTests
    {
        [Test]
        public void SimultaneousJoinHoldsAppearInFreeSeatsAndDisappearWhenReleased()
        {
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/ArenaMenu.uxml");
            VisualElement root = layout.CloneTree();
            using var view = new LobbyView(root);
            var state = new LobbyState
            {
                IsVisible = true,
                Players = new[] { new LobbyPlayerState { SelectionJoined = true } },
                JoinHolds = new[]
                {
                    new LobbyJoinHoldState { DisplayName = "Алекс", Progress = 0.5f },
                    new LobbyJoinHoldState { DisplayName = "Контроллер 12", Progress = 0.25f }
                }
            };
            view.Render(state);
            Assert.That(root.Q("lobbyJoinHold0").ClassListContains("hidden"), Is.True);
            Assert.That(root.Q("lobbyJoinHold1").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q("lobbyJoinHold1").Q<HoldProgressRing>(), Is.Not.Null);
            Assert.That(root.Q("lobbyJoinHold1").Q<Label>().text, Is.EqualTo("Алекс · 50%"));
            Assert.That(root.Q("lobbyJoinHold2").Q<Label>().text, Is.EqualTo("Контроллер 12 · 25%"));
            state.JoinHolds = System.Array.Empty<LobbyJoinHoldState>();
            view.Render(state);
            Assert.That(root.Q("lobbyJoinHold1").ClassListContains("hidden"), Is.True);
            Assert.That(root.Q("lobbyJoinHold2").ClassListContains("hidden"), Is.True);
        }

        [Test]
        public void SharedPaletteStaysOutsideCardsAndConnectedParticipantOccupiesCardOnlyAfterJoiningSelection()
        {
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/ArenaMenu.uxml");
            VisualElement root = layout.CloneTree();
            using var view = new LobbyView(root);
            var state = new LobbyState
            {
                IsVisible = true,
                MouseLocalIndex = 0,
                Players = new[] { new LobbyPlayerState { DisplayName = "Алекс", IsLocal = true, IsInputAvailable = true } },
                Characters = new[] { new LobbyCharacterState { Name = "Универсал", Description = "Без модификаторов" } }
            };
            view.Render(state);
            Assert.That(root.Q<Label>("lobbyParticipant0").text, Does.Contain("Алекс"));
            Assert.That(root.Q("lobbyCharacter0").ClassListContains("hidden"), Is.True);
            Assert.That(root.Q("lobbyEmpty0").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q("lobbyCharacter0").Query<Button>().ToList(), Is.Empty);
            Assert.That(root.Q<Button>("lobbyChoice0").parent.ClassListContains("lobby-character-choices"), Is.True);
            state.Players[0].SelectionJoined = true;
            state.Players[0].IsReady = true;
            view.Render(state);
            Assert.That(root.Q("lobbyCharacter0").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q("lobbyCharacter0").Q(className: "lobby-ready-check").ClassListContains("hidden"), Is.False);
        }
    }
}
