using System;
using System.Collections.Generic;

namespace HordeMuncher.Client.Menu
{
    public sealed class LobbyJoinHoldTracker
    {
        public const float HOLD_DURATION = 0.6f;

        private readonly Dictionary<(int, int), float> _holdStartedAt = new Dictionary<(int, int), float>();
        private readonly HashSet<(int, int)> _completedHolds = new HashSet<(int, int)>();

        public bool UpdateHold(int keyboardLayout, int deviceId, bool held, float currentTime)
        {
            var key = (keyboardLayout, deviceId);
            if (!held)
            {
                _holdStartedAt.Remove(key);
                _completedHolds.Remove(key);
                return false;
            }

            if (_completedHolds.Contains(key))
            {
                return false;
            }

            if (!_holdStartedAt.TryGetValue(key, out float startedAt))
            {
                _holdStartedAt[key] = currentTime;
                return false;
            }

            if (currentTime - startedAt < HOLD_DURATION)
            {
                return false;
            }

            _completedHolds.Add(key);
            return true;
        }

        public float ReadProgress(int keyboardLayout, int deviceId, float currentTime)
        {
            if (!_holdStartedAt.TryGetValue((keyboardLayout, deviceId), out float startedAt))
            {
                return 0;
            }

            return Math.Clamp((currentTime - startedAt) / HOLD_DURATION, 0, 1);
        }

        public void Clear()
        {
            _holdStartedAt.Clear();
            _completedHolds.Clear();
        }
    }
}
