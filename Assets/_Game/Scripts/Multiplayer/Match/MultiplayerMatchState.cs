using System;
using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public enum MultiplayerMatchState : byte
    {
        Waiting,
        Starting,
        Playing,
        Ending,
        ReturningToLobby
    }

    public enum MultiplayerSceneMode : byte
    {
        SinglePlayer,
        MultiplayerHost,
        MultiplayerClient
    }

    public enum NetworkMonsterKind : byte
    {
        Tiptoe,
        Statue
    }

    public enum NetworkMonsterEvent : byte
    {
        None,
        Aggro,
        ChaseStart,
        SearchStart,
        ReturnToRoam,
        AwarenessStop,
        Teleport,
        Reset,
        Kill
    }

    public struct PlayerRelocationCommand : INetworkSerializable, IEquatable<PlayerRelocationCommand>
    {
        public uint Sequence;
        public Vector3 Position;
        public float Yaw;
        public double ProtectionEndTime;
        public bool Fade;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref ProtectionEndTime);
            serializer.SerializeValue(ref Fade);
        }

        public bool Equals(PlayerRelocationCommand other)
        {
            return Sequence == other.Sequence && Position == other.Position && Mathf.Approximately(Yaw, other.Yaw) &&
                   ProtectionEndTime.Equals(other.ProtectionEndTime) && Fade == other.Fade;
        }
    }
}
