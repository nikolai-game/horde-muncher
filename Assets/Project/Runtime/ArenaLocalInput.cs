using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Processors;

namespace HordeMuncher
{
    public static class ArenaLocalInput
    {
        private static readonly StickDeadzoneProcessor _stickDeadzone = new StickDeadzoneProcessor
        {
            min = 0.15f,
            max = 0.95f
        };

        public static bool TryGetBinding(Unity.Entities.World world, out LocalPlayerInput input)
        {
            input = default;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            using Unity.Entities.EntityQuery query = world.EntityManager.CreateEntityQuery(typeof(LocalPlayerInput));
            if (query.CalculateEntityCount() != 1)
            {
                return false;
            }

            input = query.GetSingleton<LocalPlayerInput>();
            return true;
        }

        public static string GetName(LocalPlayerInput input)
        {
            if (input.KeyboardLayout != 0)
            {
                return input.KeyboardLayout == 1 ? "WASD" : "Стрелки";
            }

            InputDevice device = InputSystem.GetDeviceById(input.DeviceId);
            return (device is Gamepad ? "Геймпад " : "Джойстик ") + input.DeviceId;
        }

        public static bool IsAvailable(LocalPlayerInput input)
        {
            InputDevice device = input.KeyboardLayout != 0 ? Keyboard.current : InputSystem.GetDeviceById(input.DeviceId);
            return device != null && device.added && device.enabled;
        }

        public static float2 Read(LocalPlayerInput input, bool isApplicationFocused)
        {
            InputDevice device = InputSystem.GetDeviceById(input.DeviceId);
            return ReadDevices(input.KeyboardLayout == 0 ? null : Keyboard.current, device as Gamepad, device as Joystick,
                input.KeyboardLayout == 2, isApplicationFocused);
        }

        public static Vector2 ReadNavigation(LocalPlayerInput input, bool isApplicationFocused)
        {
            if (!isApplicationFocused || !IsAvailable(input))
            {
                return Vector2.zero;
            }

            Vector2 direction = Read(input, true);
            if (input.KeyboardLayout == 0 && InputSystem.GetDeviceById(input.DeviceId) is Gamepad gamepad)
            {
                Vector2 dpad = gamepad.dpad.ReadValue();
                if (dpad.sqrMagnitude > 0.25f)
                {
                    direction = dpad;
                }
            }

            return direction;
        }

        public static bool WasSubmitPressed(LocalPlayerInput input)
        {
            return WasSubmitPressed(input, Application.isFocused);
        }

        public static bool WasSubmitPressed(LocalPlayerInput input, bool isApplicationFocused)
        {
            if (!IsAvailable(input) || !isApplicationFocused)
            {
                return false;
            }

            if (input.KeyboardLayout != 0)
            {
                return input.KeyboardLayout == 1 ? Keyboard.current.eKey.wasPressedThisFrame
                    : Keyboard.current.enterKey.wasPressedThisFrame;
            }

            InputDevice device = InputSystem.GetDeviceById(input.DeviceId);
            return device is Gamepad gamepad ? gamepad.buttonSouth.wasPressedThisFrame
                : device is Joystick joystick && joystick.trigger.wasPressedThisFrame;
        }

        public static bool WasLockPressed(LocalPlayerInput input, bool isApplicationFocused)
        {
            if (!IsAvailable(input) || !isApplicationFocused || input.KeyboardLayout == 0)
            {
                return false;
            }

            return input.KeyboardLayout == 1 ? Keyboard.current.fKey.wasPressedThisFrame
                : Keyboard.current.periodKey.wasPressedThisFrame;
        }

        public static bool WasRerollPressed(LocalPlayerInput input, bool isApplicationFocused)
        {
            if (!IsAvailable(input) || !isApplicationFocused || input.KeyboardLayout == 0)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            return input.KeyboardLayout == 1
                ? keyboard.leftShiftKey.isPressed && keyboard.rKey.wasPressedThisFrame
                : keyboard.rightShiftKey.isPressed && keyboard.slashKey.wasPressedThisFrame;
        }

        public static float2 Read(bool isSecondLocalPlayer, bool isApplicationFocused)
        {
            int deviceSlot = isSecondLocalPlayer ? 1 : 0;
            Gamepad gamepad = Gamepad.all.Count > deviceSlot ? Gamepad.all[deviceSlot] : null;
            Joystick joystick = null;
            if (gamepad == null)
            {
                int joystickIndex = 0;
                foreach (InputDevice device in InputSystem.devices)
                {
                    if (device is Joystick candidate && joystickIndex++ == deviceSlot)
                    {
                        joystick = candidate;
                        break;
                    }
                }
            }

            return ReadDevices(Keyboard.current, gamepad, joystick, isSecondLocalPlayer, isApplicationFocused);
        }

        public static float2 ReadDevices(Keyboard keyboard, Gamepad gamepad, Joystick joystick, bool isSecondLocalPlayer, bool isApplicationFocused)
        {
            if (!isApplicationFocused)
            {
                return float2.zero;
            }

            float2 keyboardInput = default;
            if (keyboard != null && keyboard.added && keyboard.enabled)
            {
                keyboardInput = isSecondLocalPlayer
                    ? new float2((keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0),
                    (keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.downArrowKey.isPressed ? 1 : 0))
                    : new float2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            }

            Vector2 rawStickInput = Vector2.zero;
            if (gamepad != null && gamepad.added && gamepad.enabled)
            {
                rawStickInput = gamepad.leftStick.ReadUnprocessedValue();
            }
            else if (joystick != null && joystick.added && joystick.enabled)
            {
                rawStickInput = joystick.stick.ReadUnprocessedValue();
            }

            float2 stickInput = _stickDeadzone.Process(rawStickInput);
            float2 movementInput = math.lengthsq(keyboardInput) > math.lengthsq(stickInput) ? keyboardInput : stickInput;
            return movementInput / math.max(1f, math.length(movementInput));
        }
    }
}
