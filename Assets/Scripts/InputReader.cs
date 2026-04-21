using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kart
{
    /// <summary>
    /// 输入读取器。
    /// 基于 ScriptableObject 的输入管理器，负责将底层输入转换为游戏逻辑可用的数据。
    /// </summary>
    [CreateAssetMenu(fileName = "InputReader", menuName = "Kart/Input Reader")]
    public class InputReader : ScriptableObject, PlayerInputActions.IPlayerActions, IDrive
    {
        // IDrive 接口属性：获取移动输入（x=转向, y=油门）
        // 直接从 Input Action 中读取当前值
        public Vector2 Move => inputActions.Player.Move.ReadValue<Vector2>();

        // IDrive 接口属性：获取刹车状态
        // 当刹车动作的读取值大于 0 时（即按键被按下），返回 true
        public bool IsBraking => inputActions.Player.Brake.ReadValue<float>() > 0;
        
        // 自动生成的输入动作类实例
        private PlayerInputActions inputActions;

        // 脚本启用时（例如挂载到物体上或激活时）
        private void OnEnable()
        {
            if (inputActions == null)
            {
                // 实例化输入动作资产
                inputActions = new PlayerInputActions();
                // 将回调接口注册到 Player 动作映射中
                // 这样当 Player 映射中的动作触发时，会调用本类的 OnMove/OnBrake 等方法
                inputActions.Player.SetCallbacks(this);
            }
            // 启用输入监听
            inputActions.Enable();
        }
        
        // 脚本禁用时（例如物体销毁或场景切换）
        private void OnDisable()
        {
            if (inputActions == null) return;
            // 停止监听，节省资源
            inputActions.Disable();
        }

        // 对象销毁时
        private void OnDestroy()
        {
            if (inputActions == null) return;
            // 释放输入系统资源
            inputActions.Dispose();
            inputActions = null;
        }

        // IDrive 接口方法：启用输入
        public void Enable()
        {
            inputActions?.Enable();
        }

        // --- 输入回调方法 (由 Input System 自动调用) ---
        // 注意：这里使用了空实现 (noop)，意味着当前逻辑仅使用 ReadValue 轮询，
        // 而不依赖回调事件驱动。但这保留了扩展“按下/松开瞬间”逻辑的能力。

        public void OnMove(InputAction.CallbackContext context) { }

        public void OnLook(InputAction.CallbackContext context) { }

        public void OnFire(InputAction.CallbackContext context) { }

        public void OnBrake(InputAction.CallbackContext context) { }
    }
}