using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class MultiplayerSpawnPoint : MonoBehaviour
    {
        [SerializeField, Range(0, MultiplayerConstants.MaxPlayers - 1)] private int index;
        public int Index => index;
        public void Configure(int value) => index = Mathf.Clamp(value, 0, MultiplayerConstants.MaxPlayers - 1);
    }
}
