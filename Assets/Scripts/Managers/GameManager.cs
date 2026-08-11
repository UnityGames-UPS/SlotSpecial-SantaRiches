using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Temporary state required by SocketIOManager. These members can be replaced
    // with the full game-flow implementation later.
    public bool isInitialized;
    public bool initializationFailed;
    public PlayerData playerData = new PlayerData();
    public GameConfig gameConfig = new GameConfig();
    public int currentBetIndex;
    public double currentBetAmount;

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
    }

    public void OnInitDataReceived(
        GameConfig config,
        PlayerData initialPlayerData,
        List<List<int>> initialMatrix)
    {
    }

    public void OnSpinResultReceived(SpinResult result)
    {
    }
}
