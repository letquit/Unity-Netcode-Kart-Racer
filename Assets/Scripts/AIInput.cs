using System;
using UnityEngine;
using Utilities;

namespace Kart
{
    /// <summary>
    /// AI 输入控制器。
    /// 模拟玩家输入，控制赛车沿赛道行驶。
    /// </summary>
    public class AIInput : MonoBehaviour, IDrive
    {
        public Circuit circuit; // 赛道信息（包含路点）
        public AIDriverData driverData; // AI 驾驶配置数据
        
        // IDrive 接口实现：输出移动指令（x=转向, y=油门）
        public Vector2 Move { get; private set; }
        // IDrive 接口实现：是否刹车
        public bool IsBraking { get; private set; }
        
        public void Enable() { /* 空实现 */ }

        private int currentWaypointIndex; // 当前目标路点的索引
        private int currentCornerIndex;   // 当前目标弯道的索引（用于提前刹车）

        private CountdownTimer driftTimer; // 控制漂移（刹车）时长的定时器

        private float previousYaw;  // 上一帧的车头朝向（用于计算角速度）

        public void AddDriverData(AIDriverData data) => driverData = data;
        public void AddCircuit(Circuit circuit) => this.circuit = circuit;

        private void Start()
        {
            // 确保必要组件已赋值
            if (circuit == null || driverData == null)
            {
                throw new ArgumentNullException($"AIInput requires a circuit and driver data to be set.");
            }

            previousYaw = transform.eulerAngles.y;
            // 初始化漂移定时器，绑定刹车事件
            driftTimer = new CountdownTimer(driverData.timeToDrift);
            driftTimer.OnTimerStart += () => IsBraking = true;   // 开始漂移时刹车
            driftTimer.OnTimerStop += () => IsBraking = false;   // 漂移结束时松开刹车
        }

        private void Update()
        {
            driftTimer.Tick(Time.deltaTime); // 更新定时器
            
            if (circuit.waypoints.Length == 0) return;

            // --- 1. 计算角速度 (用于检测失控) ---
            float currentYaw = transform.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(previousYaw, currentYaw); 
            float angularVelocity = deltaYaw / Time.deltaTime; // 角速度 = 角度变化 / 时间
            previousYaw = currentYaw;
            
            // --- 2. 计算距离 ---
            Vector3 toNextPoint = circuit.waypoints[currentWaypointIndex].position - transform.position;
            Vector3 toNextCorner = circuit.waypoints[currentCornerIndex].position - transform.position;
            var distanceToNextPoint = toNextPoint.magnitude;
            var distanceToNextCorner = toNextCorner.magnitude;
            
            // --- 3. 更新路点 ---
            // 如果到达当前路点范围内，切换到下一个路点
            if (distanceToNextPoint < driverData.proximityThreshold)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % circuit.waypoints.Length;
            }
            
            // --- 4. 更新弯道目标 ---
            // 如果接近弯道范围，更新当前弯道索引（用于预测性刹车）
            if (distanceToNextCorner < driverData.updateCornerRange)
            {
                currentCornerIndex = currentWaypointIndex;
            }
            
            // --- 5. 触发漂移 ---
            // 如果接近弯道且定时器未在运行，开始漂移（刹车）
            if (distanceToNextCorner < driverData.brakeRange && !driftTimer.IsRunning)
            {
                driftTimer.Start();
            }
            
            // --- 6. 控制速度 ---
            // 如果正在漂移，降低速度；否则全速
            Move = Move.With(y: driftTimer.IsRunning ? driverData.speedWhileDrifting : 1f);
            
            // --- 7. 控制转向 ---
            Vector3 desiredForward = toNextPoint.normalized; // 期望朝向（指向下一个路点）
            Vector3 currentForward = transform.forward;      // 当前朝向
            float turnAngle = Vector3.SignedAngle(currentForward, desiredForward, Vector3.up);

            // 根据夹角决定转向：左转、右转或回正
            Move = turnAngle switch
            {
                > 5f => Move.With(x: 1f),   // 目标在右侧，向右转
                < -5f => Move.With(x: -1f), // 目标在左侧，向左转
                _ => Move.With(x: 0f)       // 目标在正前方，回正
            };
            
            // --- 8. 防失控修正 (Counter-steer) ---
            // 如果角速度过大（正在打滑旋转），反向打方向并强制刹车
            if (Mathf.Abs(angularVelocity) > driverData.spinThreshold)
            {
                // 简单的反向打盘逻辑：利用正弦函数模拟反向修正
                Move = Move.With(x: -Mathf.Sin(angularVelocity));
                IsBraking = true;
            }
            else
            {
                // 如果没有失控，刹车状态由漂移逻辑控制
                IsBraking = false;
            }
        }
    }
}