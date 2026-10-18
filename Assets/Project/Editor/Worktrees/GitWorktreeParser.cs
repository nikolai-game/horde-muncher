using System;
using System.Collections.Generic;
namespace HordeMuncher.Editor.Worktrees
{
    public static class GitWorktreeParser
    {
        public static List<GitWorktreeInfo> Parse(string output)
        {
            var worktrees = new List<GitWorktreeInfo>();
            GitWorktreeInfo current = null;
            foreach (string field in output.Split('\0'))
            {
                if (field.StartsWith("worktree ", StringComparison.Ordinal))
                {
                    current = new GitWorktreeInfo { RootPath = field.Substring(9) };
                    worktrees.Add(current);
                }
                else if (current != null)
                {
                    if (field.StartsWith("HEAD ", StringComparison.Ordinal))
                    {
                        current.Commit = field.Substring(5);
                    }
                    else if (field.StartsWith("branch refs/heads/", StringComparison.Ordinal))
                    {
                        current.Branch = field.Substring(18);
                    }
                    else if (field == "bare")
                    {
                        current.IsBare = true;
                    }
                    else if (field == "locked" || field.StartsWith("locked ", StringComparison.Ordinal))
                    {
                        current.IsLocked = true;
                    }
                    else if (field == "prunable" || field.StartsWith("prunable ", StringComparison.Ordinal))
                    {
                        current.IsPrunable = true;
                    }
                }
            }

            return worktrees;
        }
    }
}
