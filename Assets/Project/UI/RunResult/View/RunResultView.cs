using System;
using HordeMuncher.UI.Contracts;
using UnityEngine.UIElements;

namespace HordeMuncher.UI.RunResult
{
    public sealed class RunResultView : IRunResultView, IDisposable
    {
        public event Action<int> OnRestartReadinessClicked;
        private readonly VisualElement _root;
        private readonly Button _firstPlayerRestart;
        private readonly Button _secondPlayerRestart;
        private readonly Button _thirdPlayerRestart;
        private readonly Button _fourthPlayerRestart;
        private IVisualElementScheduledItem _focusRestartButton;
        private bool _wasVisible;

        public RunResultView(VisualElement root)
        {
            _root = root;
            _firstPlayerRestart = root.Q<Button>("restartButton");
            _secondPlayerRestart = root.Q<Button>("restartSecondButton");
            _thirdPlayerRestart = root.Q<Button>("restartThirdButton");
            _fourthPlayerRestart = root.Q<Button>("restartFourthButton");
            _firstPlayerRestart.clicked += FirstPlayerRestartClickedHandler;
            _secondPlayerRestart.clicked += SecondPlayerRestartClickedHandler;
            if (_thirdPlayerRestart != null)
            {
                _thirdPlayerRestart.clicked += ThirdPlayerRestartClickedHandler;
            }
            if (_fourthPlayerRestart != null)
            {
                _fourthPlayerRestart.clicked += FourthPlayerRestartClickedHandler;
            }
            _root.RegisterCallback<NavigationSubmitEvent>(NavigationSubmitHandler, TrickleDown.TrickleDown);
            Render(new RunResultViewData());
        }

        public void Render(RunResultViewData viewData)
        {
            if (viewData.IsVisible && !_wasVisible)
            {
                _focusRestartButton = _root.schedule.Execute(FocusFirstRestartButton);
            }
            else if (!viewData.IsVisible)
            {
                _focusRestartButton?.Pause();
            }

            _wasVisible = viewData.IsVisible;
            _root.EnableInClassList("hidden", !viewData.IsVisible);
            _root.Q<Label>("runResultTitle").text = viewData.Title;
            _root.Q<Label>("runResultStatus").text = viewData.Status;
            RenderButton(_firstPlayerRestart, viewData.RestartButtons, 0);
            RenderButton(_secondPlayerRestart, viewData.RestartButtons, 1);
            if (_thirdPlayerRestart != null)
            {
                RenderButton(_thirdPlayerRestart, viewData.RestartButtons, 2);
            }
            if (_fourthPlayerRestart != null)
            {
                RenderButton(_fourthPlayerRestart, viewData.RestartButtons, 3);
            }
        }

        public void Dispose()
        {
            _focusRestartButton?.Pause();
            _firstPlayerRestart.clicked -= FirstPlayerRestartClickedHandler;
            _secondPlayerRestart.clicked -= SecondPlayerRestartClickedHandler;
            if (_thirdPlayerRestart != null)
            {
                _thirdPlayerRestart.clicked -= ThirdPlayerRestartClickedHandler;
            }
            if (_fourthPlayerRestart != null)
            {
                _fourthPlayerRestart.clicked -= FourthPlayerRestartClickedHandler;
            }
            _root.UnregisterCallback<NavigationSubmitEvent>(NavigationSubmitHandler, TrickleDown.TrickleDown);
            _root.EnableInClassList("hidden", true);
        }

        private void FocusFirstRestartButton()
        {
            Button firstAvailable = _root.Query<Button>().ToList().Find(button => button.enabledInHierarchy
                && !button.ClassListContains("hidden"));
            firstAvailable?.Focus();
        }

        private void FirstPlayerRestartClickedHandler()
        {
            OnRestartReadinessClicked?.Invoke(0);
        }

        private void SecondPlayerRestartClickedHandler()
        {
            OnRestartReadinessClicked?.Invoke(1);
        }

        private void ThirdPlayerRestartClickedHandler()
        {
            OnRestartReadinessClicked?.Invoke(2);
        }

        private void FourthPlayerRestartClickedHandler()
        {
            OnRestartReadinessClicked?.Invoke(3);
        }

        private void NavigationSubmitHandler(NavigationSubmitEvent navigation)
        {
            navigation.StopImmediatePropagation();
        }

        private static void RenderButton(Button button, RunResultButtonData[] buttons, int index)
        {
            RunResultButtonData buttonData = index < buttons.Length ? buttons[index] : default;
            button.EnableInClassList("hidden", !buttonData.IsVisible);
            button.SetEnabled(buttonData.IsVisible && buttonData.IsEnabled);
            button.text = buttonData.Text ?? "";
        }
    }
}
