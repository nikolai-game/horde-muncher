using System;
using HordeMuncher.UI.Contracts;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.Hud
{
    public sealed class PlayerSeparationWarningPresenter : IDisposable
    {
        private readonly IPlayerSeparationWarningStateSource _stateSource;
        private readonly VisualElement _warning;

        public PlayerSeparationWarningPresenter(IPlayerSeparationWarningStateSource stateSource, VisualElement warning)
        {
            _stateSource = stateSource;
            _warning = warning;
            Refresh();
        }

        public void Refresh()
        {
            _warning.EnableInClassList("hidden", !_stateSource.IsWarningVisible);
        }

        public void Dispose()
        {
            _warning.EnableInClassList("hidden", true);
        }
    }
}
