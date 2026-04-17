using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using Utilities;
using Random = UnityEngine.Random;

namespace Kart
{
    public class KartSpawner : MonoBehaviour
    {
        [SerializeField] private Circuit circuit;
        [SerializeField] private AIDriverData aiDriverData;
        [SerializeField] private GameObject[] aiKartPrefabs;

        [SerializeField] private GameObject playerKartPrefab;
        [SerializeField] private CinemachineVirtualCamera playerCamera;

        [Header("Grid Randomization")]
        [SerializeField] private bool randomizeGrid = true;
        [SerializeField] private bool randomizePlayerPosition = true;
        [SerializeField] private int seed = -1;

        private void Start()
        {
            var slots = new List<Transform>(circuit.spawnPoints);

            if (seed >= 0) Random.InitState(seed);

            if (randomizeGrid)
                Shuffle(slots);

            int playerSlotIndex = 0;
            if (randomizePlayerPosition)
                playerSlotIndex = Random.Range(0, slots.Count);

            Transform playerSpawn = slots[playerSlotIndex];
            var playerKart = Instantiate(playerKartPrefab, playerSpawn.position, playerSpawn.rotation);

            if (playerCamera != null)
            {
                playerCamera.Follow = playerKart.transform;
                playerCamera.LookAt = playerKart.transform;
            }

            slots.RemoveAt(playerSlotIndex);

            for (int i = 0; i < slots.Count; i++)
            {
                var aiPrefab = aiKartPrefabs[Random.Range(0, aiKartPrefabs.Length)];

                new AIKartBuilder(aiPrefab)
                    .WithDriverData(aiDriverData)
                    .WithCircuit(circuit)
                    .WithSpawnPoint(slots[i])
                    .Build();
            }
        }

        private static void Shuffle(List<Transform> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private class AIKartBuilder
        {
            private readonly GameObject prefab;
            private AIDriverData data;
            private Circuit circuit;
            private Transform spawnPoint;

            public AIKartBuilder(GameObject prefab)
            {
                this.prefab = prefab;
            }

            public AIKartBuilder WithDriverData(AIDriverData data)
            {
                this.data = data;
                return this;
            }

            public AIKartBuilder WithCircuit(Circuit circuit)
            {
                this.circuit = circuit;
                return this;
            }

            public AIKartBuilder WithSpawnPoint(Transform spawnPoint)
            {
                this.spawnPoint = spawnPoint;
                return this;
            }

            public GameObject Build()
            {
                var instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
                var aiInput = instance.GetOrAdd<AIInput>();
                aiInput.AddCircuit(circuit);
                aiInput.AddDriverData(data);

                instance.GetComponent<KartController>().SetInput(aiInput);

                return instance;
            }
        }
    }
}