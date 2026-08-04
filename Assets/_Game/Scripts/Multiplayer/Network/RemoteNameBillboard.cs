using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class RemoteNameBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera target = Camera.main;
            if (target == null) return;
            Vector3 direction = transform.position - target.transform.position;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
