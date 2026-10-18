using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
namespace HordeMuncher.Editor.Worktrees
{
    public sealed class GitWorktreeService : IWorktreeService
    {
        private const int GIT_TIMEOUT_MILLISECONDS = 5000;
        private readonly string _editorPath;
        public string CurrentProjectPath { get; private set; }

        public GitWorktreeService(string projectPath, string editorPath)
        {
            CurrentProjectPath = Path.GetFullPath(projectPath);
            _editorPath = editorPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(editorPath, "Contents", "MacOS", "Unity") : editorPath;
        }

        public Task<IReadOnlyList<GitWorktreeInfo>> ReadAsync()
        {
            return Task.Run<IReadOnlyList<GitWorktreeInfo>>(() =>
            {
                string repositoryRoot = RunGit("rev-parse --show-toplevel").TrimEnd('\r', '\n');
                string projectSubdirectory = RunGit("rev-parse --show-prefix").TrimEnd('\r', '\n');
                CurrentProjectPath = Path.GetFullPath(Path.Combine(repositoryRoot, projectSubdirectory));
                List<GitWorktreeInfo> worktrees = GitWorktreeParser.Parse(RunGit("worktree list --porcelain -z"));
                foreach (GitWorktreeInfo worktree in worktrees)
                {
                    worktree.ProjectPath = Path.GetFullPath(Path.Combine(worktree.RootPath, projectSubdirectory));
                }

                return worktrees;
            });
        }

        public bool IsCurrentProject(string projectPath)
        {
            StringComparison comparison = Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Path.GetFullPath(projectPath), CurrentProjectPath, comparison);
        }

        public bool CanOpenProject(GitWorktreeInfo worktree)
        {
            return !worktree.IsBare && !worktree.IsPrunable
                && Directory.Exists(Path.Combine(worktree.ProjectPath, "Assets"))
                && File.Exists(Path.Combine(worktree.ProjectPath, "ProjectSettings", "ProjectVersion.txt"));
        }

        public void OpenProject(GitWorktreeInfo worktree)
        {
            if (IsCurrentProject(worktree.ProjectPath) || !CanOpenProject(worktree))
            {
                throw new InvalidOperationException("Этот worktree уже открыт здесь или не содержит доступного Unity-проекта.");
            }

            string versionFile = Path.Combine(worktree.ProjectPath, "ProjectSettings", "ProjectVersion.txt");
            string versionLine = "m_EditorVersion: " + Application.unityVersion;
            if (!File.ReadAllLines(versionFile).Contains(versionLine))
            {
                throw new InvalidOperationException("У проекта другая версия Unity. Откройте его через Unity Hub с нужной версией Editor.");
            }

            if (!File.Exists(_editorPath))
            {
                throw new FileNotFoundException("Не найден исполняемый файл Unity Editor: " + _editorPath);
            }

            string logDirectory = Path.Combine(worktree.ProjectPath, "Logs");
            Directory.CreateDirectory(logDirectory);
            var startInfo = new ProcessStartInfo
            {
                FileName = _editorPath,
                UseShellExecute = false,
                WorkingDirectory = worktree.ProjectPath
            };
            startInfo.ArgumentList.Add("-projectPath");
            startInfo.ArgumentList.Add(worktree.ProjectPath);
            startInfo.ArgumentList.Add("-logFile");
            startInfo.ArgumentList.Add(Path.Combine(logDirectory, "WorktreeEditor.log"));
            using Process process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Не удалось запустить Unity Editor.");
            }
        }

        public void RevealFolder(GitWorktreeInfo worktree)
        {
            if (!Directory.Exists(worktree.RootPath))
            {
                throw new DirectoryNotFoundException("Папка worktree недоступна. Обновите список.");
            }

            EditorUtility.RevealInFinder(worktree.RootPath);
        }

        private string RunGit(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = File.Exists("/usr/bin/git") ? "/usr/bin/git" : "git",
                Arguments = arguments,
                WorkingDirectory = CurrentProjectPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using Process process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Не удалось запустить Git. Проверьте установку Git.");
            }

            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(GIT_TIMEOUT_MILLISECONDS))
            {
                process.Kill();
                throw new TimeoutException("Git не ответил за 5 секунд. Попробуйте обновить список.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Не удалось прочитать Git worktree: " + error.GetAwaiter().GetResult().Trim());
            }

            return output.GetAwaiter().GetResult();
        }
    }
}
