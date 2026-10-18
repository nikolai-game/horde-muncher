
namespace HordeMuncher.Editor.Worktrees
{
    public sealed class GitWorktreeInfo
    {
        public string RootPath;
        public string ProjectPath;
        public string Branch;
        public string Commit;
        public bool IsBare;
        public bool IsLocked;
        public bool IsPrunable;
    }
}
