using UnityEngine;

namespace Kart
{
    /// <summary>
    /// AI 赛车手数据配置。
    /// 使用 ScriptableObject 存储 AI 驾驶参数，方便在编辑器中调整。
    /// </summary>
    [CreateAssetMenu(fileName = "AIDriverData", menuName = "Kart/AIDriverData")]
    public class AIDriverData : ScriptableObject
    {
        [Tooltip("判定到达路点的距离阈值。当 AI 与路点的距离小于此值时，视为已到达。")]
        public float proximityThreshold = 20.0f;

        [Tooltip("更新弯道目标路点的距离。当接近弯道此范围内时，AI 会更新下一个目标点。")]
        public float updateCornerRange = 50f;

        [Tooltip("刹车距离。当距离弯道还有这么远时，AI 开始减速。")]
        public float brakeRange = 80f;

        [Tooltip("防失控角速度阈值。当车辆旋转角速度超过此值时，AI 会尝试反向打方向盘修正。")]
        public float spinThreshold = 100f;

        [Tooltip("漂移时的速度系数（0-1）。例如 0.5 表示保持最高速度的一半。")]
        public float speedWhileDrifting = 0.5f;

        [Tooltip("漂移持续时间或相关的时间系数。")]
        public float timeToDrift = 0.5f;
    }
}