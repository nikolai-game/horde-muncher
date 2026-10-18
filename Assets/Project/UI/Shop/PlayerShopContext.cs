using System;
using HordeMuncher.UI.Shop.Presentation;
using HordeMuncher.UI.Shop.View;
using Unity.Entities;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace HordeMuncher.Client.Shop
{
    public sealed class PlayerShopContext : IDisposable
    {

        public World World { get; }
        public int LocalIndex { get; }
        public int PlayerNumber { get; }
        public ClientShopGateway Gateway { get; }
        public ShopPresenter Presenter { get; }
        public ShopView View { get; }
        public ShopNavigationInput Input { get; }

        public PlayerShopContext(World world,
            int localIndex,
            int playerNumber,
            VisualElement parent,
            ShopIconResolver iconResolver,
            Action exitToMenuRequestedHandler)
        {
            World = world;
            LocalIndex = localIndex;
            PlayerNumber = playerNumber;
            Gateway = new ClientShopGateway(world, playerNumber);
            View = new ShopView(parent, iconResolver);
            Presenter = new ShopPresenter(Gateway, Gateway, View);
            Presenter.OnExitToMenuRequested += exitToMenuRequestedHandler;
            LocalPlayerInput input = ArenaLocalInput.TryGetBinding(world, out LocalPlayerInput binding)
                ? binding : new LocalPlayerInput { LocalIndex = localIndex, KeyboardLayout = localIndex == 1
                    ? LocalInputLayout.SECONDARY_KEYBOARD : 1 };
            string controllerSubmitHint = InputSystem.GetDeviceById(input.DeviceId) is Gamepad gamepad
                ? gamepad.buttonSouth.displayName : "Trigger";
            string submitHint = input.KeyboardLayout == LocalInputLayout.PRIMARY_KEYBOARD
                ? "E" : input.KeyboardLayout == LocalInputLayout.SECONDARY_KEYBOARD ? "Enter" : controllerSubmitHint;
            string readyHoldHint = input.KeyboardLayout == LocalInputLayout.PRIMARY_KEYBOARD
                ? "Shift+E" : input.KeyboardLayout == LocalInputLayout.SECONDARY_KEYBOARD ? "Right Shift+Enter"
                : InputSystem.GetDeviceById(input.DeviceId) is Gamepad readyGamepad
                    ? readyGamepad.leftTrigger.displayName + "+" + readyGamepad.buttonSouth.displayName : "LT+A";
            View.SetControlHints(submitHint, input.KeyboardLayout == LocalInputLayout.PRIMARY_KEYBOARD
                ? "F" : input.KeyboardLayout == LocalInputLayout.SECONDARY_KEYBOARD ? "." : "",
                input.KeyboardLayout == LocalInputLayout.PRIMARY_KEYBOARD
                    ? "Shift+R" : input.KeyboardLayout == LocalInputLayout.SECONDARY_KEYBOARD ? "Shift+/" : submitHint,
                input.KeyboardLayout == LocalInputLayout.PRIMARY_KEYBOARD
                    ? "Tab" : input.KeyboardLayout == LocalInputLayout.SECONDARY_KEYBOARD ? "Right Ctrl"
                    : InputSystem.GetDeviceById(input.DeviceId) is Gamepad hintGamepad ? hintGamepad.rightShoulder.displayName : submitHint,
                readyHoldHint,
                input.KeyboardLayout == LocalInputLayout.PRIMARY_KEYBOARD ? "R" : input.KeyboardLayout == LocalInputLayout.SECONDARY_KEYBOARD ? "/"
                    : InputSystem.GetDeviceById(input.DeviceId) is Gamepad mergeGamepad ? mergeGamepad.buttonWest.displayName : "X");
            Input = localIndex >= 0 ? new ShopNavigationInput(input, Presenter) : null;
        }

        public void Dispose()
        {
            Presenter.Dispose();
            View.Dispose();
            Gateway.Dispose();
        }
    }
}
