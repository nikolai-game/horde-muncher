using HordeMuncher.Client.Menu;
using NUnit.Framework;

namespace HordeMuncher.Tests.Menu
{
    public sealed class LobbyJoinHoldTests
    {
        [Test]
        public void HoldRequiresDurationAndReleaseBeforeJoiningAgain()
        {
            var holds = new LobbyJoinHoldTracker();
            Assert.That(holds.UpdateHold(1, 0, true, 0), Is.False);
            Assert.That(holds.UpdateHold(1, 0, true, 0.3f), Is.False);
            Assert.That(holds.UpdateHold(1, 0, false, 0.4f), Is.False);
            Assert.That(holds.UpdateHold(1, 0, true, 0.5f), Is.False);
            Assert.That(holds.UpdateHold(1, 0, true, 1.2f), Is.True);
            Assert.That(holds.UpdateHold(1, 0, true, 2), Is.False);
            Assert.That(holds.UpdateHold(1, 0, false, 3), Is.False);
            Assert.That(holds.UpdateHold(1, 0, true, 4), Is.False);
            Assert.That(holds.UpdateHold(1, 0, true, 5), Is.True);
        }

        [Test]
        public void ProgressTracksEachHoldAndResetsOnReleaseAndClear()
        {
            var holds = new LobbyJoinHoldTracker();
            holds.UpdateHold(1, 0, true, 1);
            holds.UpdateHold(0, 12, true, 1.15f);
            Assert.That(holds.ReadProgress(1, 0, 1.3f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(holds.ReadProgress(0, 12, 1.3f), Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(holds.ReadProgress(1, 0, 2), Is.EqualTo(1));
            holds.UpdateHold(1, 0, false, 2);
            Assert.That(holds.ReadProgress(1, 0, 2), Is.Zero);
            holds.Clear();
            Assert.That(holds.ReadProgress(0, 12, 2), Is.Zero);
        }

        [Test]
        public void DevicesAndKeyboardLayoutsHaveIndependentHoldTimers()
        {
            var holds = new LobbyJoinHoldTracker();
            holds.UpdateHold(1, 0, true, 0);
            holds.UpdateHold(2, 0, true, 0.5f);
            holds.UpdateHold(0, 12, true, 0.5f);
            Assert.That(holds.UpdateHold(1, 0, true, 0.7f), Is.True);
            Assert.That(holds.UpdateHold(2, 0, true, 0.7f), Is.False);
            Assert.That(holds.UpdateHold(0, 12, true, 0.7f), Is.False);
            Assert.That(holds.UpdateHold(2, 0, true, 1.2f), Is.True);
            Assert.That(holds.UpdateHold(0, 12, true, 1.2f), Is.True);
        }
    }
}
