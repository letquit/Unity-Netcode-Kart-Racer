using System;
using Eflatun.SceneReference;
using UnityEngine;
using UnityEngine.UI;

namespace Kart
{
    /// <summary>
    /// 大厅用户界面控制器。
    /// 处理玩家创建或加入游戏的 UI 交互逻辑。
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private Button createLobbyButton; // "创建游戏" 按钮
        [SerializeField] private Button joinLobbyButton;   // "加入游戏" 按钮
        [SerializeField] private SceneReference gameScene; // 目标游戏场景引用

        /// <summary>
        /// 初始化阶段，绑定按钮事件。
        /// </summary>
        private void Awake()
        {
            createLobbyButton.onClick.AddListener(CreateGame);
            joinLobbyButton.onClick.AddListener(JoinGame);
        }

        /// <summary>
        /// 创建游戏逻辑。
        /// 异步创建大厅，成功后加载游戏场景。
        /// </summary>
        private async void CreateGame()
        {
            // 1. 调用多人服务创建大厅
            // await 确保等待网络请求完成
            await Multiplayer.Instance.CreateLobby();
            
            // 2. 大厅创建成功后，通过网络加载游戏场景
            Loader.LoadNetwork(gameScene);
        }
  
        /// <summary>
        /// 加入游戏逻辑。
        /// 快速加入一个现有的大厅。
        /// </summary>
        private async void JoinGame()
        {
            await Multiplayer.Instance.QuickJoinLobby();
            // 注意：通常加入成功后也需要加载场景，这里可能由 Multiplayer 内部处理或后续补充
        }
    }
}