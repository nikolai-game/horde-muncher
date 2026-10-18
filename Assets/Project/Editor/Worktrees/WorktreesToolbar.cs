using System;
using System.IO;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace HordeMuncher.Editor.Worktrees
{
    [InitializeOnLoad]
    public static class WorktreesToolbar
    {
        private const string ELEMENT_PATH = "Horde Muncher/Current Git Branch";
        private const double REFRESH_INTERVAL_SECONDS = 3;
        private const int MAX_BRANCH_LABEL_LENGTH = 32;
        private static readonly GitWorktreeService _service;
        private static string _branchLabel = "Git…";
        private static string _tooltip = "Текущая ветка · нажмите, чтобы открыть worktree";
        private static double _nextRefreshTime;
        private static bool _refreshing;

        static WorktreesToolbar()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            _service = new GitWorktreeService(projectPath, EditorApplication.applicationPath);
            EditorApplication.update += UpdateBranch;
            EditorApplication.focusChanged += EditorFocusChangedHandler;
        }

        private static void EditorFocusChangedHandler(bool focused)
        {
            if (focused)
            {
                _nextRefreshTime = 0;
            }
        }

        private static void UpdateBranch()
        {
            if (_refreshing || !EditorApplication.isFocused || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.timeSinceStartup < _nextRefreshTime)
            {
                return;
            }

            RefreshBranchAsync();
        }

        private static async void RefreshBranchAsync()
        {
            _refreshing = true;
            try
            {
                System.Collections.Generic.IReadOnlyList<GitWorktreeInfo> worktrees = await _service.ReadAsync();
                string branchLabel = "Git: нет ветки";
                foreach (GitWorktreeInfo worktree in worktrees)
                {
                    if (!_service.IsCurrentProject(worktree.ProjectPath))
                    {
                        continue;
                    }

                    branchLabel = worktree.Branch;
                    if (string.IsNullOrEmpty(branchLabel))
                    {
                        string commit = worktree.Commit ?? "";
                        branchLabel = "Detached · " + commit.Substring(0, Math.Min(8, commit.Length));
                    }

                    break;
                }

                UpdateContent(branchLabel, branchLabel + "\n" + _service.CurrentProjectPath + "\nНажмите, чтобы открыть worktree");
            }
            catch (Exception exception)
            {
                UpdateContent("Git недоступен", exception.Message + "\nНажмите, чтобы открыть Worktrees");
            }
            finally
            {
                _refreshing = false;
                _nextRefreshTime = EditorApplication.timeSinceStartup + REFRESH_INTERVAL_SECONDS;
            }
        }

        private static void UpdateContent(string branchLabel, string tooltip)
        {
            if (_branchLabel == branchLabel && _tooltip == tooltip)
            {
                return;
            }

            _branchLabel = branchLabel;
            _tooltip = tooltip;
            MainToolbar.Refresh(ELEMENT_PATH);
        }

        [MainToolbarElement(ELEMENT_PATH, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 1)]
        private static MainToolbarDropdown CreateBranchDropdown()
        {
            string label = _branchLabel.Length <= MAX_BRANCH_LABEL_LENGTH
                ? _branchLabel : _branchLabel.Substring(0, MAX_BRANCH_LABEL_LENGTH - 1) + "…";
            return new MainToolbarDropdown(new MainToolbarContent(label, _tooltip), WorktreesWindow.OpenDropdown);
        }
    }
}
