using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class PlayerAuthenticationService : MonoBehaviour
    {
        private Task signInTask;
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        internal string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

        public Task SignInAsync()
        {
            return signInTask ??= SignInInternalAsync();
        }

        private async Task SignInInternalAsync()
        {
            if (IsSignedIn) return;
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            if (!IsSignedIn) throw new InvalidOperationException("Anonymous sign-in did not complete.");
            Debug.Log("UNITY_ANONYMOUS_AUTH_SUCCESS");
        }
    }
}
