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
                ? binding : new LocalPlayerInput { LocalIndex = localIndex, KeyboardLayout = localIndex == 1 ? 2 : 1 };
            string controllerSubmitHint = InputSystem.GetDeviceById(input.DeviceId) is Gamepad gamepad
                ? gamepad.buttonSouth.displayName : "Trigger";
            string submitHint = input.KeyboardLayout == 1 ? "E" : input.KeyboardLayout == 2 ? "Enter" : controllerSubmitHint;
            string readyHoldHint = input.KeyboardLayout == 1 ? "Shift+E" : input.KeyboardLayout == 2 ? "Right Shift+Enter"
                : InputSystem.GetDeviceById(input.DeviceId) is Gamepad readyGamepad
                    ? readyGamepad.leftTrigger.displayName + "+" + readyGamepad.buttonSouth.displayName : "LT+A";
            View.SetControlHints(submitHint, input.KeyboardLayout == 1 ? "F" : input.KeyboardLayout == 2 ? "." : "",
                input.KeyboardLayout == 1 ? "Shift+R" : input.KeyboardLayout == 2 ? "Shift+/" : submitHint,
                input.KeyboardLayout == 1 ? "Tab" : input.KeyboardLayout == 2 ? "Right Ctrl"
                    : InputSystem.GetDeviceById(input.DeviceId) is Gamepad hintGamepad ? hintGamepad.rightShoulder.displayName : submitHint,
                readyHoldHint,
                input.KeyboardLayout == 1 ? "R" : input.KeyboardLayout == 2 ? "/"
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
