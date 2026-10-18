using System.Collections.Generic;
using System.Threading.Tasks;
namespace HordeMuncher.Editor.Worktrees
{
    public interface IWorktreeService
    {
        string CurrentProjectPath { get; }
        Task<IReadOnlyList<GitWorktreeInfo>> ReadAsync();
        bool IsCurrentProject(string projectPath);
        bool CanOpenProject(GitWorktreeInfo worktree);
        void OpenProject(GitWorktreeInfo worktree);
        void RevealFolder(GitWorktreeInfo worktree);
    }
}
