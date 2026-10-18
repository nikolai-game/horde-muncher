using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace HordeMuncher.Editor.Worktrees
{
    public sealed class WorktreesWindow : EditorWindow, IWorktreesView
    {
        private const string LAYOUT_PATH = "Assets/Project/Editor/Worktrees/WorktreesWindow.uxml";
        public event Action OnRefreshClicked;
        public event Action<string> OnOpenUnityClicked;
        public event Action<string> OnRevealFolderClicked;
        private WorktreesPresenter _presenter;
        private Button _refreshButton;
        private Label _status;
        private ScrollView _worktrees;

        private void OnDisable()
        {
            _presenter?.Dispose();
            _presenter = null;
            if (_refreshButton != null)
            {
                _refreshButton.clicked -= RefreshClickedHandler;
            }
        }

        public async void CreateGUI()
        {
            _presenter?.Dispose();
            rootVisualElement.Clear();
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LAYOUT_PATH);
            if (layout == null)
            {
                rootVisualElement.Add(new Label("Не найдена разметка Worktrees: " + LAYOUT_PATH));
                return;
            }

            layout.CloneTree(rootVisualElement);
            _refreshButton = rootVisualElement.Q<Button>("refreshWorktrees");
            _status = rootVisualElement.Q<Label>("worktreesStatus");
            _worktrees = rootVisualElement.Q<ScrollView>("worktreesList");
            _refreshButton.clicked += RefreshClickedHandler;
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            var service = new GitWorktreeService(projectPath, EditorApplication.applicationPath);
            _presenter = new WorktreesPresenter(service, this);
            await _presenter.RefreshAsync();
        }

        public void SetRefreshing(bool refreshing)
        {
            _refreshButton.SetEnabled(!refreshing);
            _worktrees.SetEnabled(!refreshing);
        }

        public void ShowStatus(string message, bool isError)
        {
            _status.text = message;
            _status.EnableInClassList("worktrees-error", isError);
        }

        public void Render(IReadOnlyList<WorktreeRowState> rows)
        {
            _worktrees.Clear();
            foreach (WorktreeRowState row in rows)
            {
                string projectPath = row.ProjectPath;
                var card = new VisualElement();
                card.AddToClassList("worktree-card");
                var branch = new Label(row.Branch);
                branch.AddToClassList("worktree-branch");
                card.Add(branch);
                var path = new Label(projectPath) { tooltip = projectPath, selection = { isSelectable = true } };
                path.AddToClassList("worktree-path");
                card.Add(path);
                if (!string.IsNullOrEmpty(row.Status))
                {
                    card.Add(new Label(row.Status));
                }

                var actions = new VisualElement();
                actions.AddToClassList("worktree-actions");
                var open = new Button(() => OnOpenUnityClicked?.Invoke(projectPath)) { text = "Открыть в новом Unity" };
                open.SetEnabled(row.CanOpen);
                open.tooltip = row.CanOpen ? "Запустить отдельный Unity Editor для этой рабочей копии"
                    : "Текущий проект или недоступная рабочая копия";
                actions.Add(open);
                var reveal = new Button(() => OnRevealFolderClicked?.Invoke(projectPath)) { text = "Показать в Finder" };
                reveal.SetEnabled(row.CanReveal);
                actions.Add(reveal);
                card.Add(actions);
                _worktrees.Add(card);
            }
        }

        private void RefreshClickedHandler()
        {
            OnRefreshClicked?.Invoke();
        }

        public static void OpenDropdown(Rect buttonRect)
        {
            var window = CreateInstance<WorktreesWindow>();
            window.titleContent = new GUIContent("Worktrees");
            window.ShowAsDropDown(buttonRect, new Vector2(620, 400));
        }

        [MenuItem("Horde Muncher/Worktrees")]
        public static void OpenWindow()
        {
            var window = GetWindow<WorktreesWindow>("Worktrees");
            window.minSize = new Vector2(560, 260);
        }
    }
}
