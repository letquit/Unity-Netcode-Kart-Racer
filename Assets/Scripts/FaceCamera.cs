using UnityEngine;

namespace Kart 
{
    /// <summary>
    /// 面向摄像机脚本（广告牌效果）。
    /// 强制物体旋转角度与摄像机保持一致，常用于世界空间 UI 或 2D 精灵。
    /// </summary>
    public class FaceCamera : MonoBehaviour 
    {
        // 目标摄像机引用。如果不设置，通常默认为主摄像机，但这里显式赋值更安全。
        [SerializeField] Transform kartCamera; 

        void Update() 
        {
            // 确保摄像机引用有效
            if (kartCamera) 
            {
                // 将当前物体的旋转设置为与摄像机完全一致
                // 这样物体就会始终“正对”着摄像机
                transform.rotation = kartCamera.rotation;
            }
        }
    }
}