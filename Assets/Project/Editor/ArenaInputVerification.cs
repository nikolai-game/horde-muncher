using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HordeMuncher.Editor
{
    public static class ArenaInputVerification
    {
        [MenuItem("Horde Muncher/Verify Controller Input")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Run controller verification outside Play Mode.");
            }

            Gamepad firstGamepad = null;
            Gamepad secondGamepad = null;
            Keyboard keyboard = null;
            Joystick joystick = null;
            int checks = 0;
            void Check(bool condition, string message)
            {
                if (!condition)
                {
                    throw new Exception(message);
                }

                checks++;
            }

            float2 ReadPlayerInput(Gamepad gamepad, bool focused = true) => ArenaLocalInput.ReadDevices(keyboard, gamepad, null, false, focused);
            void SetGamepadStick(Gamepad gamepad, Vector2 stickInput)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState
                {
                    leftStick = stickInput
                });
                InputSystem.Update();
            }

            try
            {
                firstGamepad = InputSystem.AddDevice<Gamepad>("ArenaVerificationFirst");
                secondGamepad = InputSystem.AddDevice<Gamepad>("ArenaVerificationSecond");
                keyboard = InputSystem.AddDevice<Keyboard>("ArenaVerificationKeyboard");
                joystick = InputSystem.AddDevice<Joystick>("ArenaVerificationJoystick");
                SetGamepadStick(firstGamepad, new Vector2(0.1f, 0));
                Check(math.lengthsq(ReadPlayerInput(firstGamepad)) == 0, "Stick drift escaped the dead zone.");
                SetGamepadStick(firstGamepad, new Vector2(0.55f, 0));
                Check(math.distance(ReadPlayerInput(firstGamepad), new float2(0.5f, 0)) < 0.001f, "Analog speed lost.");
                SetGamepadStick(secondGamepad, new Vector2(0, 1));
                Check(ReadPlayerInput(firstGamepad).y == 0 && ReadPlayerInput(secondGamepad).x == 0 && ReadPlayerInput(secondGamepad).y > 0.99f,
                    "Controllers share input.");
                SetGamepadStick(firstGamepad, Vector2.one);
                Check(math.abs(math.length(ReadPlayerInput(firstGamepad)) - 1) < 0.001f, "Diagonal speed boost.");
                Check(math.lengthsq(ReadPlayerInput(firstGamepad, false)) == 0, "Input persisted after loss of focus.");
                SetGamepadStick(firstGamepad, Vector2.zero);
                Check(math.lengthsq(ReadPlayerInput(firstGamepad)) == 0, "Movement persisted after releasing stick.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
                InputSystem.Update();
                Check(math.abs(math.length(ReadPlayerInput(firstGamepad)) - 1) < 0.001f && ReadPlayerInput(firstGamepad).y > 0,
                    "Keyboard fallback failed.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueDeltaStateEvent(joystick.stick, new Vector2(0, -0.55f));
                InputSystem.Update();
                float2 joystickInput = ArenaLocalInput.ReadDevices(keyboard, null, joystick, false, true);
                Check(math.distance(joystickInput, new float2(0, -0.5f)) < 0.001f, "Generic joystick fallback failed.");
                InputSystem.RemoveDevice(firstGamepad);
                Check(math.lengthsq(ReadPlayerInput(firstGamepad)) == 0, "Disconnected gamepad retained movement.");
                Debug.Log($"Arena controller verification: {checks} checks passed.");
            }
            finally
            {
                if (firstGamepad != null && firstGamepad.added)
                {
                    InputSystem.RemoveDevice(firstGamepad);
                }

                if (secondGamepad != null && secondGamepad.added)
                {
                    InputSystem.RemoveDevice(secondGamepad);
                }

                if (keyboard != null && keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                if (joystick != null && joystick.added)
                {
                    InputSystem.RemoveDevice(joystick);
                }
            }
        }
    }
}
