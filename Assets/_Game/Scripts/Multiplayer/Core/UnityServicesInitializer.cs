using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class UnityServicesInitializer : MonoBehaviour
    {
        private Task initializationTask;
        public bool IsReady { get; private set; }
        public bool HasCloudProjectId => !string.IsNullOrWhiteSpace(Application.cloudProjectId);

        public Task InitializeAsync()
        {
            return initializationTask ??= InitializeInternalAsync();
        }

        private async Task InitializeInternalAsync()
        {
            if (!HasCloudProjectId)
            {
                throw new InvalidOperationException("This build is not linked to a Unity Cloud Project. Open Edit > Project Settings > Services and link the project before using online rooms.");
            }
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                IsReady = true;
                return;
            }
            await UnityServices.InitializeAsync();
            IsReady = UnityServices.State == ServicesInitializationState.Initialized;
            if (!IsReady) throw new InvalidOperationException("Unity Gaming Services did not finish initializing.");
            Debug.Log("UNITY_SERVICES_INITIALIZED");
        }
    }
}
