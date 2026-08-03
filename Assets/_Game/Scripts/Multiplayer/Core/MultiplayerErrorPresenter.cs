using System;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerErrorPresenter : MonoBehaviour
    {
        public event Action<string> StatusChanged;
        public event Action<string> ErrorChanged;
        public string Status { get; private set; } = "Ready";
        public string Error { get; private set; } = string.Empty;

        public void SetStatus(string message)
        {
            Status = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
            StatusChanged?.Invoke(Status);
        }

        public void ShowError(string message)
        {
            Error = string.IsNullOrWhiteSpace(message) ? "Something went wrong." : message;
            ErrorChanged?.Invoke(Error);
            Debug.LogWarning($"MULTIPLAYER_ERROR: {Error}");
        }

        public void ClearError()
        {
            Error = string.Empty;
            ErrorChanged?.Invoke(Error);
        }
    }
}
