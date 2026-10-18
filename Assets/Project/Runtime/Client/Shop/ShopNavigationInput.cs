using HordeMuncher.UI.Shop.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HordeMuncher.Client.Shop
{
    public sealed class ShopNavigationInput
    {
        private readonly LocalPlayerInput _input;
        private readonly ShopPresenter _presenter;
        private float _nextMove;

        public ShopNavigationInput(LocalPlayerInput input, ShopPresenter presenter)
        {
            _input = input;
            _presenter = presenter;
        }

        public void Update(float time)
        {
            if (!Application.isFocused || !_presenter.IsVisible)
            {
                _presenter.UpdateReadyHold(false, time);
                return;
            }

            bool isReadyHeld = IsReadyHeld();
            _presenter.UpdateReadyHold(isReadyHeld, time);
            if (isReadyHeld)
            {
                return;
            }

            Vector2 direction = ArenaLocalInput.ReadNavigation(_input, true);

            if (direction.sqrMagnitude < 0.25f)
            {
                _nextMove = 0;
            }
            else if (time >= _nextMove)
            {
                _presenter.MoveSelection(direction.x, direction.y);
                _nextMove = time + 0.22f;
            }

            if (WasStatsPressed())
            {
                _presenter.ToggleStats();
            }
            else if (ArenaLocalInput.WasRerollPressed(_input, true))
            {
                _presenter.RerollShop();
            }
            else if (WasMergePressed())
            {
                _presenter.MergeSelectedWeapon();
            }
            else if (ArenaLocalInput.WasLockPressed(_input, true))
            {
                _presenter.LockSelectedOffer();
            }
            else if (ArenaLocalInput.WasSubmitPressed(_input))
            {
                _presenter.SubmitSelection();
            }
        }

        public void CancelReadyHold(float time)
        {
            _presenter.UpdateReadyHold(false, time);
        }

        private bool IsReadyHeld()
        {
            if (!ArenaLocalInput.IsAvailable(_input))
            {
                return false;
            }

            if (_input.KeyboardLayout != 0)
            {
                Keyboard keyboard = Keyboard.current;
                return _input.KeyboardLayout == 1
                    ? keyboard.leftShiftKey.isPressed && keyboard.eKey.isPressed
                    : keyboard.rightShiftKey.isPressed && keyboard.enterKey.isPressed;
            }

            return InputSystem.GetDeviceById(_input.DeviceId) is Gamepad gamepad
                && gamepad.leftTrigger.isPressed && gamepad.buttonSouth.isPressed;
        }

        private bool WasMergePressed()
        {
            if (!ArenaLocalInput.IsAvailable(_input))
            {
                return false;
            }

            if (_input.KeyboardLayout != 0)
            {
                Keyboard keyboard = Keyboard.current;
                return _input.KeyboardLayout == 1
                    ? !keyboard.leftShiftKey.isPressed && keyboard.rKey.wasPressedThisFrame
                    : !keyboard.rightShiftKey.isPressed && keyboard.slashKey.wasPressedThisFrame;
            }

            return InputSystem.GetDeviceById(_input.DeviceId) is Gamepad gamepad && gamepad.buttonWest.wasPressedThisFrame;
        }

        private bool WasStatsPressed()
        {
            if (!ArenaLocalInput.IsAvailable(_input))
            {
                return false;
            }

            if (_input.KeyboardLayout != 0)
            {
                return _input.KeyboardLayout == 1 ? Keyboard.current.tabKey.wasPressedThisFrame
                    : Keyboard.current.rightCtrlKey.wasPressedThisFrame;
            }

            return InputSystem.GetDeviceById(_input.DeviceId) is Gamepad gamepad && gamepad.rightShoulder.wasPressedThisFrame;
        }
    }
}
