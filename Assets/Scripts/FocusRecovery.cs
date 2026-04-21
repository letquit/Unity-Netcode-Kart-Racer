using UnityEngine;
using UnityEngine.EventSystems; // 引入 UI 事件系统，用于重置选中状态
using UnityEngine.InputSystem; // 引入新输入系统
using UnityEngine.InputSystem.LowLevel; // 引入底层输入接口，用于强制刷新状态

/// <summary>
/// 输入焦点恢复脚本。
/// 解决 Unity 新输入系统在窗口失焦后重新获得焦点时可能出现的输入卡死问题。
/// </summary>
public class FocusRecovery : MonoBehaviour
{
    // 是否在重新获得焦点时解锁鼠标光标（通常用于调试或窗口化模式）
    [SerializeField] private bool relockCursorOnFocus = true;

    /// <summary>
    /// 当应用程序获得或失去焦点时调用
    /// </summary>
    /// <param name="hasFocus">是否获得焦点</param>
    private void OnApplicationFocus(bool hasFocus)
    {
        // 只有当窗口重新获得焦点时才执行修复逻辑
        if (!hasFocus) return;

        // --- 1. 强制刷新输入系统状态 ---
        // 创建一个空的键盘状态事件并推入队列，这会强制输入系统重新读取所有按键状态
        // 这可以解决“按键粘滞”问题（即切屏前按下的键在切屏后仍被视为按下）
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        // 立即处理队列中的事件，确保状态立即更新
        InputSystem.Update();

        // --- 2. 重置玩家输入组件 ---
        // 查找场景中的 PlayerInput 组件
        var playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null && !string.IsNullOrEmpty(playerInput.currentActionMap?.name))
        {
            // 重新激活输入组件，确保输入逻辑重新连接
            playerInput.ActivateInput();
            // 重新启用当前的动作映射（如 "Driving", "UI" 等）
            playerInput.currentActionMap.Enable();
        }

        // --- 3. 清理 UI 选中状态 ---
        // 清除 EventSystem 的选中对象，防止 UI 交互状态残留
        // 例如：防止切屏前选中的按钮在切屏后仍然处于选中状态，拦截游戏输入
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // --- 4. 管理鼠标光标 ---
        if (relockCursorOnFocus)
        {
            // 解锁鼠标并显示光标，方便玩家在窗口化模式下操作
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}