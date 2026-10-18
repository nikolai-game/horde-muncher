using System;
using System.Collections.Generic;
namespace HordeMuncher.Editor.Worktrees
{
    public interface IWorktreesView
    {
        event Action OnRefreshClicked;
        event Action<string> OnOpenUnityClicked;
        event Action<string> OnRevealFolderClicked;
        void SetRefreshing(bool refreshing);
        void ShowStatus(string message, bool isError);
        void Render(IReadOnlyList<WorktreeRowState> rows);
    }
}
