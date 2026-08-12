using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Shared state and typed events connecting the socket layer to the slot view.
    public bool isInitialized;
    public bool initializationFailed;
    public PlayerData playerData = new PlayerData();
    public GameConfig gameConfig = new GameConfig();
    public int currentBetIndex;
    public double currentBetAmount;

    public event Action<GameConfig, PlayerData, List<List<int>>> InitDataReceived;
    public event Action<SpinResult> SpinResultReceived;
    public event Action Disconnected;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnDisconnected()
    {
        Disconnected?.Invoke();
    }

    public void OnInitDataReceived(
        GameConfig config,
        PlayerData initialPlayerData,
        List<List<int>> initialMatrix)
    {
        gameConfig = config;
        playerData = initialPlayerData;
        isInitialized = config != null && initialPlayerData != null;
        initializationFailed = !isInitialized;
        InitDataReceived?.Invoke(config, initialPlayerData, initialMatrix);
    }

    public void OnSpinResultReceived(SpinResult result)
    {
        if (result?.playerData != null)
        {
            playerData = result.playerData;
            currentBetIndex = result.playerData.currentBetIndex;
        }

        SpinResultReceived?.Invoke(result);
    }
}
