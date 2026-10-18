using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HordeMuncher.Tests.Menu
{
    public sealed class LocalUiShortcutTests : InputTestFixture
    {
        [TestCase(Key.E, true, false, false, false)]
        [TestCase(Key.Enter, false, true, false, false)]
        [TestCase(Key.F, false, false, true, false)]
        [TestCase(Key.Period, false, false, false, true)]
        [TestCase(Key.NumpadEnter, false, false, false, false)]
        public void KeyboardActionsBelongOnlyToTheAssignedPlayer(Key key,
            bool firstSubmit, bool secondSubmit, bool firstLock, bool secondLock)
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                InputSystem.Update();
                var first = new LocalPlayerInput { KeyboardLayout = 1 };
                var second = new LocalPlayerInput { KeyboardLayout = 2 };

                Assert.That(ArenaLocalInput.WasSubmitPressed(first, true), Is.EqualTo(firstSubmit));
                Assert.That(ArenaLocalInput.WasSubmitPressed(second, true), Is.EqualTo(secondSubmit));
                Assert.That(ArenaLocalInput.WasLockPressed(first, true), Is.EqualTo(firstLock));
                Assert.That(ArenaLocalInput.WasLockPressed(second, true), Is.EqualTo(secondLock));
                Assert.That(ArenaLocalInput.WasSubmitPressed(first, false), Is.False);
                Assert.That(ArenaLocalInput.WasLockPressed(second, false), Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [TestCase(Key.LeftShift, Key.R, true, false)]
        [TestCase(Key.RightShift, Key.Slash, false, true)]
        [TestCase(Key.RightShift, Key.R, false, false)]
        [TestCase(Key.LeftShift, Key.Slash, false, false)]
        [TestCase(Key.None, Key.R, false, false)]
        [TestCase(Key.None, Key.Slash, false, false)]
        public void RerollRequiresTheCorrectShiftAndDoesNotRepeatWhileHeld(Key modifier, Key key,
            bool firstReroll, bool secondReroll)
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                KeyboardState keys = modifier == Key.None ? new KeyboardState(key) : new KeyboardState(modifier, key);
                InputSystem.QueueStateEvent(keyboard, keys);
                InputSystem.Update();
                var first = new LocalPlayerInput { KeyboardLayout = 1 };
                var second = new LocalPlayerInput { KeyboardLayout = 2 };

                Assert.That(ArenaLocalInput.WasRerollPressed(first, true), Is.EqualTo(firstReroll));
                Assert.That(ArenaLocalInput.WasRerollPressed(second, true), Is.EqualTo(secondReroll));
                Assert.That(ArenaLocalInput.WasRerollPressed(first, false), Is.False);
                Assert.That(ArenaLocalInput.WasRerollPressed(second, false), Is.False);

                InputSystem.QueueStateEvent(keyboard, keys);
                InputSystem.Update();
                Assert.That(ArenaLocalInput.WasRerollPressed(first, true), Is.False);
                Assert.That(ArenaLocalInput.WasRerollPressed(second, true), Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
