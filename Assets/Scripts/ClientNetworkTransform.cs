using Unity.Netcode.Components;
using UnityEngine;

namespace Kart
{
    public enum LegacyAuthorityMode
    {
        Server,
        Client
    }

    [DisallowMultipleComponent]
    public class NetworkTransformAuthorityBridge : MonoBehaviour
    {
        public LegacyAuthorityMode authorityMode = LegacyAuthorityMode.Client;

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

        private void Apply()
        {
            if (!target) return;
            target.AuthorityMode = authorityMode == LegacyAuthorityMode.Server
                ? NetworkTransform.AuthorityModes.Server
                : NetworkTransform.AuthorityModes.Owner;
        }
    }
}