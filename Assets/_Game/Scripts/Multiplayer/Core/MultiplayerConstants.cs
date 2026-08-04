namespace TheBestMonkeyGame.Multiplayer
{
    public static class MultiplayerConstants
    {
        public const string ApplicationVersion = "0.7.0";
        public const string NetworkVersion = "tbmg-net-2";
        public const string MainMenuScene = "MainMenu";
        public const string SinglePlayerScene = "MainMap";
        public const string LobbyScene = "MultiplayerLobby";
        public const int MaxPlayers = 4;
        public const int PoseSendRate = 20;
        public const ushort LocalTestPort = 7777;
        public const float JoinRequestCooldownSeconds = 2f;
        public const string NetworkVersionProperty = "network-version";
        public const string CustomRoomCodeProperty = "custom-room-code";
        public const string MatchStateProperty = "match-state";
    }
}
