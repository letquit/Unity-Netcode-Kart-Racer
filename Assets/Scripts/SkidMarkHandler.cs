using UnityEngine;

namespace Kart {
    /// <summary>
    /// 轮胎印迹处理器。
    /// 根据轮胎的滑移程度动态生成和销毁轮胎印迹特效。
    /// </summary>
    public class SkidMarkHandler : MonoBehaviour {
        [SerializeField] private float slipThreshold = 0.4f; // 触发印迹的滑移阈值
        [SerializeField] private Transform skidMarkPrefab;   // 轮胎印迹预制体
        private KartController kart;                         // 赛车控制器引用

        private WheelCollider[] wheelColliders; // 所有车轮碰撞体
        private Transform[] skidMarks = new Transform[4]; // 当前激活的印迹对象数组（对应4个轮子）

        private void Start() {
            kart = GetComponent<KartController>();
            // 获取挂载在子物体中的所有 WheelCollider
            wheelColliders = GetComponentsInChildren<WheelCollider>();
        }

        private void Update() {
            // 每一帧检查所有车轮的状态
            for (var i = 0; i < wheelColliders.Length; i++) {
                UpdateSkidMarks(i);
            }
        }

        /// <summary>
        /// 更新单个车轮的印迹状态
        /// </summary>
        /// <param name="i">车轮索引</param>
        private void UpdateSkidMarks(int i) {
            // 1. 获取地面接触信息
            // 如果车轮悬空（未接触地面）或赛车未着地，则结束印迹
            if (!wheelColliders[i].GetGroundHit(out var hit) || !kart.IsGrounded) {
                EndSkid(i);
                return;
            }

            // 2. 检查滑移量
            // 如果侧滑或纵向滑移超过阈值，开始生成印迹；否则结束
            if (Mathf.Abs(hit.sidewaysSlip) > slipThreshold || Mathf.Abs(hit.forwardSlip) > slipThreshold) {
                StartSkid(i);
            } else {
                EndSkid(i);
            }
        }

        /// <summary>
        /// 开始生成印迹（激活）
        /// </summary>
        /// <param name="i">车轮索引</param>
        private void StartSkid(int i) {
            // 如果当前没有印迹，则实例化一个新的
            if (skidMarks[i] == null) {
                skidMarks[i] = Instantiate(skidMarkPrefab, wheelColliders[i].transform);
                // 调整位置到轮胎底部（半径 * 0.9 稍微嵌入地面一点以防穿模）
                skidMarks[i].localPosition = -Vector3.up * wheelColliders[i].radius * .9f;
                // 旋转至水平朝下
                skidMarks[i].localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        /// <summary>
        /// 结束印迹（销毁/留在地面）
        /// </summary>
        /// <param name="i">车轮索引</param>
        private void EndSkid(int i) {
            // 如果当前有印迹，将其从车轮解绑并留在原地，5秒后销毁
            if (skidMarks[i] != null) {
                Transform holder = skidMarks[i];
                skidMarks[i] = null; // 重置引用
                
                holder.SetParent(null); // 解除父子关系，留在世界坐标
                holder.rotation = Quaternion.Euler(90f, 0f, 0f); // 确保朝向正确
                Destroy(holder.gameObject, 5f); // 5秒后销毁对象
            }
        }
    }
}