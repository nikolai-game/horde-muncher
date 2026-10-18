using System;
using System.Collections.Generic;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.UI.RunResult
{
    public sealed class RunResultPresenter : IDisposable
    {
        private const float RESPONSE_DELAY_SECONDS = 2;
        private readonly IReadOnlyList<IRunResultStateSource> _playerStateSources;
        private readonly IReadOnlyList<IRunRestartCommands> _restartCommands;
        private readonly IRunResultView _view;
        private readonly bool[] _awaitingResponse;
        private readonly RunResultState[] _sentStates;
        private readonly float[] _sentTimes;
        private float _currentTime;
        private bool _disposed;

        public RunResultPresenter(IReadOnlyList<IRunResultStateSource> playerStateSources,
            IReadOnlyList<IRunRestartCommands> restartCommands,
            IRunResultView view)
        {
            if (playerStateSources.Count != restartCommands.Count)
            {
                throw new ArgumentException("Each local player needs restart commands.");
            }

            _playerStateSources = playerStateSources;
            _restartCommands = restartCommands;
            _view = view;
            _awaitingResponse = new bool[playerStateSources.Count];
            _sentStates = new RunResultState[playerStateSources.Count];
            _sentTimes = new float[playerStateSources.Count];
            _view.OnRestartReadinessClicked += RestartReadinessClickedHandler;
        }

        public void Refresh(float currentTime)
        {
            if (_disposed)
            {
                return;
            }

            _currentTime = currentTime;
            var viewData = new RunResultViewData
            {
                RestartButtons = new RunResultButtonData[_playerStateSources.Count]
            };
            bool isResponseDelayed = false;
            for (int playerIndex = 0; playerIndex < _playerStateSources.Count; playerIndex++)
            {
                if (!_playerStateSources[playerIndex].TryRead(out RunResultState state) || !state.IsFinished)
                {
                    _awaitingResponse[playerIndex] = false;
                    continue;
                }

                if (_awaitingResponse[playerIndex] && !HasSameCommandState(state, _sentStates[playerIndex]))
                {
                    _awaitingResponse[playerIndex] = false;
                }

                if (!viewData.IsVisible)
                {
                    viewData.Title = state.Phase == RunPhase.Victory ? "Победа!" : "Забег окончен";
                    viewData.Status = "Новый забег тем же составом · готовы " + state.ReadyPlayers + "/" + state.TotalPlayers;
                }

                viewData.IsVisible = true;
                viewData.RestartButtons[playerIndex] = new RunResultButtonData
                {
                    IsVisible = true,
                    IsEnabled = !_awaitingResponse[playerIndex],
                    Text = (_playerStateSources.Count > 1 ? "Игрок " + (playerIndex + 1) + " · " : "")
                    + (state.RestartReady ? "Отменить готовность" : "Ещё раз")
                };
                isResponseDelayed |= _awaitingResponse[playerIndex] && currentTime - _sentTimes[playerIndex] >= RESPONSE_DELAY_SECONDS;
            }

            if (isResponseDelayed)
            {
                viewData.Status += " · Ожидание ответа сервера";
            }

            _view.Render(viewData);
        }

        public void ToggleRestartReadiness(int localPlayerIndex, float currentTime)
        {
            if (_disposed || localPlayerIndex < 0 || localPlayerIndex >= _playerStateSources.Count || _awaitingResponse[localPlayerIndex])
            {
                return;
            }

            if (!_playerStateSources[localPlayerIndex].TryRead(out RunResultState state) || !state.IsFinished
                || !_restartCommands[localPlayerIndex].TryToggleRestartReadiness(state.Revision))
            {
                return;
            }

            _awaitingResponse[localPlayerIndex] = true;
            _sentStates[localPlayerIndex] = state;
            _sentTimes[localPlayerIndex] = currentTime;
            Refresh(currentTime);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.OnRestartReadinessClicked -= RestartReadinessClickedHandler;
        }

        private void RestartReadinessClickedHandler(int localPlayerIndex)
        {
            ToggleRestartReadiness(localPlayerIndex, _currentTime);
        }

        private static bool HasSameCommandState(RunResultState current, RunResultState sent)
        {
            return current.PlayerNumber == sent.PlayerNumber && current.Revision == sent.Revision && current.Phase == sent.Phase
                && current.RestartReady == sent.RestartReady;
        }
    }
}
