using System;
using Unity.Netcode;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    [Serializable]
    public struct VRPoseState : INetworkSerializable, IEquatable<VRPoseState>
    {
        public Vector3 RootPosition;
        public float RootYaw;
        public Vector3 HeadPosition;
        public Quaternion HeadRotation;
        public Vector3 LeftHandPosition;
        public Quaternion LeftHandRotation;
        public Vector3 RightHandPosition;
        public Quaternion RightHandRotation;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RootPosition);
            serializer.SerializeValue(ref RootYaw);
            serializer.SerializeValue(ref HeadPosition);
            serializer.SerializeValue(ref HeadRotation);
            serializer.SerializeValue(ref LeftHandPosition);
            serializer.SerializeValue(ref LeftHandRotation);
            serializer.SerializeValue(ref RightHandPosition);
            serializer.SerializeValue(ref RightHandRotation);
        }

        public bool Equals(VRPoseState other)
        {
            return RootPosition.Equals(other.RootPosition) && RootYaw.Equals(other.RootYaw) &&
                   HeadPosition.Equals(other.HeadPosition) && HeadRotation.Equals(other.HeadRotation) &&
                   LeftHandPosition.Equals(other.LeftHandPosition) && LeftHandRotation.Equals(other.LeftHandRotation) &&
                   RightHandPosition.Equals(other.RightHandPosition) && RightHandRotation.Equals(other.RightHandRotation);
        }
    }
}
