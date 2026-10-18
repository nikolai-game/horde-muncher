using System.Collections.Generic;
using HordeMuncher.Client.Shop;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Scripting.APIUpdating;

namespace HordeMuncher
{
    [RequireComponent(typeof(UIDocument), typeof(ArenaSession))]
    [MovedFrom(true, "Brotato3D", "Brotato3D.Client.Shop", "ShopPresentation")]
    public sealed class ShopUiCompositionRoot : MonoBehaviour
    {
        private const float REFRESH_INTERVAL_SECONDS = 0.1f;

        public EconomySettings Settings;
        private readonly List<PlayerShopContext> _playerShopContexts = new List<PlayerShopContext>();
        private ArenaSession _session;
        private VisualElement _documentRoot;
        private VisualElement _overlay;
        private float _nextRefresh;

        private void OnEnable()
        {
            if (_session != null)
            {
                BindDocument();
            }
        }

        private void OnDisable()
        {
            UnbindDocument();
        }

        private void Update()
        {
            if (_session == null)
            {
                return;
            }

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root != _documentRoot)
            {
                UnbindDocument();
                BindDocument();
            }

            if (_documentRoot == null)
            {
                return;
            }

            bool isPaused = _session.IsPaused;
            _overlay.SetEnabled(!isPaused);
            if (isPaused)
            {
                foreach (PlayerShopContext context in _playerShopContexts)
                {
                    context.Input?.CancelReadyHold(Time.unscaledTime);
                }

                return;
            }

            if (Time.unscaledTime >= _nextRefresh)
            {
                RefreshPlayerShops();
                _nextRefresh = Time.unscaledTime + REFRESH_INTERVAL_SECONDS;
            }

            foreach (PlayerShopContext context in _playerShopContexts)
            {
                context.Input?.Update(Time.unscaledTime);
            }
        }

        public void Init(ArenaSession session, EconomySettings settings)
        {
            UnbindDocument();
            _session = session;
            Settings = settings;
            if (isActiveAndEnabled)
            {
                BindDocument();
            }
        }

        public void Shutdown()
        {
            UnbindDocument();
            _session = null;
        }

        private void BindDocument()
        {
            _documentRoot = GetComponent<UIDocument>().rootVisualElement;
            if (_documentRoot == null)
            {
                return;
            }

            _documentRoot.RegisterCallback<NavigationSubmitEvent>(NavigationSubmitHandler, TrickleDown.TrickleDown);
            _documentRoot.RegisterCallback<NavigationMoveEvent>(NavigationMoveHandler, TrickleDown.TrickleDown);
            _overlay = new VisualElement
            {
                name = "shopOverlay"
            };
            _overlay.AddToClassList("shop-overlay");
            _overlay.style.display = DisplayStyle.None;
            _documentRoot.Add(_overlay);
            _nextRefresh = 0;
        }

        private void UnbindDocument()
        {
            if (_documentRoot != null)
            {
                _documentRoot.UnregisterCallback<NavigationSubmitEvent>(NavigationSubmitHandler, TrickleDown.TrickleDown);
                _documentRoot.UnregisterCallback<NavigationMoveEvent>(NavigationMoveHandler, TrickleDown.TrickleDown);
            }

            foreach (PlayerShopContext context in _playerShopContexts)
            {
                context.Dispose();
            }

            _playerShopContexts.Clear();
            _overlay?.RemoveFromHierarchy();
            _overlay = null;
            _documentRoot = null;
        }

        private void RefreshPlayerShops()
        {
            List<ClientArenaPlayer> players = ClientArenaRoster.Read(_session.LocalClientWorlds);
            for (int contextIndex = _playerShopContexts.Count - 1; contextIndex >= 0; contextIndex--)
            {
                PlayerShopContext context = _playerShopContexts[contextIndex];
                if (!players.Exists(player => player.World == context.World && player.PlayerNumber == context.PlayerNumber
                    && player.LocalIndex == context.LocalIndex))
                {
                    context.Dispose();
                    _playerShopContexts.RemoveAt(contextIndex);
                }
            }

            bool visible = false;
            for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                ClientArenaPlayer player = players[playerIndex];
                PlayerShopContext context = _playerShopContexts.Find(shop => shop.PlayerNumber == player.PlayerNumber);
                if (context == null)
                {
                    context = new PlayerShopContext(player.World, player.LocalIndex, player.PlayerNumber,
                        _overlay, TryGetIcon, _session.StopSession);
                    _playerShopContexts.Add(context);
                }

                context.View.SetPlayerLayout(playerIndex, players.Count);
                context.Presenter.SetPlayerCount(players.Count);
                context.Presenter.Refresh(Time.unscaledTime);
                visible |= context.Presenter.IsVisible;
            }

            _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool TryGetIcon(int catalogId, out Sprite sprite)
        {
            sprite = null;
            return Settings != null && Settings.TryGetIcon(catalogId, out sprite);
        }

        private void NavigationSubmitHandler(NavigationSubmitEvent navigation)
        {
            if (!_session.IsPaused && _playerShopContexts.Exists(context => context.Presenter.IsVisible))
            {
                navigation.StopImmediatePropagation();
            }
        }

        private void NavigationMoveHandler(NavigationMoveEvent navigation)
        {
            if (!_session.IsPaused && _playerShopContexts.Exists(context => context.Presenter.IsVisible))
            {
                navigation.StopImmediatePropagation();
            }
        }
    }
}
