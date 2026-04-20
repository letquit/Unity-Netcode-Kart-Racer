using Unity.Netcode.Components;
using UnityEngine;

namespace Kart
{
    public enum AuthorityMode
    {
        Server,
        Client
    }

    [DisallowMultipleComponent]
    public class ClientNetworkTransform : MonoBehaviour
    {
        public AuthorityMode authorityMode = AuthorityMode.Client;

        public bool SyncPositionX = true;
        public bool SyncPositionY = true;
        public bool SyncPositionZ = true;

        [SerializeField] private NetworkTransform target;

        private void Reset()
        {
            if (!target) target = GetComponent<NetworkTransform>();
            Apply();
        }

        private void OnValidate()
        {
            if (!target) target = GetComponent<NetworkTransform>();
            Apply();
        }

        private void Awake() => Apply();

        public void Apply()
        {
            if (!target) return;

            target.AuthorityMode = authorityMode == AuthorityMode.Server
                ? NetworkTransform.AuthorityModes.Server
                : NetworkTransform.AuthorityModes.Owner;

            target.SyncPositionX = SyncPositionX;
            target.SyncPositionY = SyncPositionY;
            target.SyncPositionZ = SyncPositionZ;
        }
    }
}