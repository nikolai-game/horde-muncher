using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace HordeMuncher.Editor.Worktrees
{
    public sealed class WorktreesPresenter : IDisposable
    {
        private readonly IWorktreeService _service;
        private readonly IWorktreesView _view;
        private IReadOnlyList<GitWorktreeInfo> _worktrees = Array.Empty<GitWorktreeInfo>();
        private bool _refreshing;
        private bool _disposed;

        public WorktreesPresenter(IWorktreeService service, IWorktreesView view)
        {
            _service = service;
            _view = view;
            _view.OnRefreshClicked += RefreshClickedHandler;
            _view.OnOpenUnityClicked += OpenUnityClickedHandler;
            _view.OnRevealFolderClicked += RevealFolderClickedHandler;
        }

        public async Task RefreshAsync()
        {
            if (_refreshing || _disposed)
            {
                return;
            }

            _refreshing = true;
            _view.SetRefreshing(true);
            _view.ShowStatus("Чтение списка Git worktree…", false);
            try
            {
                IReadOnlyList<GitWorktreeInfo> worktrees = await _service.ReadAsync();
                if (_disposed)
                {
                    return;
                }

                _worktrees = worktrees;
                var rows = new List<WorktreeRowState>();
                foreach (GitWorktreeInfo worktree in worktrees)
                {
                    string branch = worktree.Branch;
                    if (string.IsNullOrEmpty(branch))
                    {
                        string commit = worktree.Commit ?? "";
                        branch = worktree.IsBare ? "Bare repository" : "Detached HEAD · " + commit.Substring(0, Math.Min(8, commit.Length));
                    }

                    bool isCurrent = _service.IsCurrentProject(worktree.ProjectPath);
                    rows.Add(new WorktreeRowState
                    {
                        ProjectPath = worktree.ProjectPath,
                        Branch = branch,
                        Status = (isCurrent ? "Текущий проект" : "")
                            + (worktree.IsLocked ? " · Git lock" : "")
                            + (worktree.IsPrunable ? " · Папка недоступна / prunable" : ""),
                        CanOpen = !isCurrent && _service.CanOpenProject(worktree),
                        CanReveal = System.IO.Directory.Exists(worktree.RootPath)
                    });
                }

                _view.Render(rows);
                _view.ShowStatus("Worktree: " + rows.Count + " · Текущий проект: " + _service.CurrentProjectPath, false);
            }
            catch (Exception exception)
            {
                if (!_disposed)
                {
                    _worktrees = Array.Empty<GitWorktreeInfo>();
                    _view.Render(Array.Empty<WorktreeRowState>());
                    _view.ShowStatus(exception.Message, true);
                }
            }
            finally
            {
                _refreshing = false;
                if (!_disposed)
                {
                    _view.SetRefreshing(false);
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _view.OnRefreshClicked -= RefreshClickedHandler;
            _view.OnOpenUnityClicked -= OpenUnityClickedHandler;
            _view.OnRevealFolderClicked -= RevealFolderClickedHandler;
        }

        private async void RefreshClickedHandler()
        {
            await RefreshAsync();
        }

        private void OpenUnityClickedHandler(string projectPath)
        {
            if (!TryGetWorktree(projectPath, out GitWorktreeInfo worktree))
            {
                return;
            }

            try
            {
                _service.OpenProject(worktree);
                _view.ShowStatus("Запуск Unity: " + projectPath + ". Первый импорт может занять несколько минут.", false);
            }
            catch (Exception exception)
            {
                _view.ShowStatus(exception.Message, true);
            }
        }

        private void RevealFolderClickedHandler(string projectPath)
        {
            if (!TryGetWorktree(projectPath, out GitWorktreeInfo worktree))
            {
                return;
            }

            try
            {
                _service.RevealFolder(worktree);
            }
            catch (Exception exception)
            {
                _view.ShowStatus(exception.Message, true);
            }
        }

        private bool TryGetWorktree(string projectPath, out GitWorktreeInfo worktree)
        {
            foreach (GitWorktreeInfo candidate in _worktrees)
            {
                if (candidate.ProjectPath == projectPath)
                {
                    worktree = candidate;
                    return true;
                }
            }

            worktree = null;
            return false;
        }
    }
}
