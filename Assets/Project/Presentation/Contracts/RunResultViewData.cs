namespace HordeMuncher.UI.Contracts
{
    public sealed class RunResultViewData
    {
        public bool IsVisible;
        public string Title = "";
        public string Status = "";
        public RunResultButtonData[] RestartButtons = System.Array.Empty<RunResultButtonData>();
    }
}
