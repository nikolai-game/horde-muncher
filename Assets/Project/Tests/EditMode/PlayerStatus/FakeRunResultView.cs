using System;
using HordeMuncher.UI.Contracts;

namespace HordeMuncher.Tests.PlayerStatus
{
    public sealed class FakeRunResultView : IRunResultView
    {
        public event Action<int> OnRestartReadinessClicked;
        public RunResultViewData Data;
        public int SubscriberCount => OnRestartReadinessClicked?.GetInvocationList().Length ?? 0;

        public void Render(RunResultViewData viewData)
        {
            Data = viewData;
        }

        public void ClickRestart(int localPlayerIndex)
        {
            OnRestartReadinessClicked?.Invoke(localPlayerIndex);
        }
    }
}
