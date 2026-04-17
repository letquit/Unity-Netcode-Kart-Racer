using UnityEngine;

namespace Kart {
    public class SkidMarkHandler : MonoBehaviour {
        [SerializeField] private float slipThreshold = 0.4f;
        [SerializeField] private Transform skidMarkPrefab;
        private KartController kart;

        private WheelCollider[] wheelColliders;
        private Transform[] skidMarks = new Transform[4];


        private void Start() {
            kart = GetComponent<KartController>();
            wheelColliders = GetComponentsInChildren<WheelCollider>();
        }

        private void Update() {
            for (var i = 0; i < wheelColliders.Length; i++) {
                UpdateSkidMarks(i);
            }
        }

        private void UpdateSkidMarks(int i) {
            if (!wheelColliders[i].GetGroundHit(out var hit) || !kart.IsGrounded) {
                EndSkid(i);
                return;
            }

            if (Mathf.Abs(hit.sidewaysSlip) > slipThreshold || Mathf.Abs(hit.forwardSlip) > slipThreshold) {
                StartSkid(i);
            } else {
                EndSkid(i);
            }
        }

        private void StartSkid(int i) {
            if (skidMarks[i] == null) {
                skidMarks[i] = Instantiate(skidMarkPrefab, wheelColliders[i].transform);
                skidMarks[i].localPosition = -Vector3.up * wheelColliders[i].radius * .9f;
                skidMarks[i].localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private void EndSkid(int i) {
            if (skidMarks[i] != null) {
                Transform holder = skidMarks[i];
                skidMarks[i] = null;
                holder.SetParent(null);
                holder.rotation = Quaternion.Euler(90f, 0f, 0f);
                Destroy(holder.gameObject, 5f);
            }
        }
    }
}