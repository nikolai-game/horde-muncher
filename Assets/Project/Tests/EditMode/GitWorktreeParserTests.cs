using HordeMuncher.Editor.Worktrees;
using NUnit.Framework;

namespace HordeMuncher.Tests
{
    public sealed class GitWorktreeParserTests
    {
        [Test]
        public void NullSeparatedRecordsPreserveSpacesUnicodeAndNewlinesInPaths()
        {
            string output = "worktree /Projects/Моя игра\0HEAD abcdef1234567890\0branch refs/heads/main\0\0"
                + "worktree /tmp/player lobby\npreview\0HEAD 0123456789abcdef\0branch refs/heads/feature/player-lobby\0\0";
            var worktrees = GitWorktreeParser.Parse(output);
            Assert.That(worktrees.Count, Is.EqualTo(2));
            Assert.That(worktrees[0].RootPath, Is.EqualTo("/Projects/Моя игра"));
            Assert.That(worktrees[0].Branch, Is.EqualTo("main"));
            Assert.That(worktrees[1].RootPath, Is.EqualTo("/tmp/player lobby\npreview"));
            Assert.That(worktrees[1].Branch, Is.EqualTo("feature/player-lobby"));
        }

        [Test]
        public void DetachedBareLockedAndPrunableWorktreesDoNotReuseAnotherBranch()
        {
            string output = "worktree /main\0HEAD aaaaaaaa\0branch refs/heads/main\0\0"
                + "worktree /detached\0HEAD bbbbbbbb\0detached\0locked keep this folder\0\0"
                + "worktree /missing\0HEAD cccccccc\0branch refs/heads/old\0prunable missing path\0\0"
                + "worktree /repository.git\0bare\0\0";
            var worktrees = GitWorktreeParser.Parse(output);
            Assert.That(worktrees[1].Branch, Is.Null);
            Assert.That(worktrees[1].Commit, Is.EqualTo("bbbbbbbb"));
            Assert.That(worktrees[1].IsLocked, Is.True);
            Assert.That(worktrees[2].IsPrunable, Is.True);
            Assert.That(worktrees[3].IsBare, Is.True);
            Assert.That(worktrees[3].Branch, Is.Null);
        }

        [Test]
        public void EmptyOutputHasNoWorktrees()
        {
            Assert.That(GitWorktreeParser.Parse(""), Is.Empty);
        }
    }
}
