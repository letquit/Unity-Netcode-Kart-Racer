using System;
using Eflatun.SceneReference;
using UnityEngine;
using UnityEngine.UI;

namespace Kart
{
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private Button createLobbyButton;
        [SerializeField] private Button joinLobbyButton;
        [SerializeField] private SceneReference gameScene;

        private void Awake()
        {
            createLobbyButton.onClick.AddListener(CreateGame);
            joinLobbyButton.onClick.AddListener(JoinGame);
        }

        private async void CreateGame()
        {
            await Multiplayer.Instance.CreateLobby();
            Loader.LoadNetwork(gameScene);
        }
  
        private async void JoinGame()
        {
            await Multiplayer.Instance.QuickJoinLobby();
        }
    }
}