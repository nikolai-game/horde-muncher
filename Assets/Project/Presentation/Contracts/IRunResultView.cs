namespace HordeMuncher.UI.Contracts
{
    public interface IRunResultView
    {
        event System.Action<int> OnRestartReadinessClicked;
        void Render(RunResultViewData viewData);
    }
}
