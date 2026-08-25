using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Best.SocketIO;
using Best.SocketIO.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class SocketIOManager : MonoBehaviour
{
    [SerializeField] private string testToken = "test-token";
    protected string testSocketURL = "https://devrealtime.dingdinghouse.com/";
    protected string nameSpace = "playground";


    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [SerializeField] private UIManager uiManager;
    [SerializeField] private PopupManager popupManager;
    [SerializeField] internal JSFunctCalls JSManager;
    [SerializeField] private GameObject RaycastBlocker;

    private SocketManager socketManager;
    private Socket gameSocket;

    private string authToken;
    private string socketURL;

    internal bool isConnected;
    internal bool isInitialized;
    internal bool isExiting;   // True after an intentional CloseGame request.
    private bool isDestroyed;  // True when scene is unloading or application is quitting
    private bool socketSetupStarted;
    private bool exitMessageSent;
    private Coroutine exitRoutine;

    private bool hasFocus = true;
    private float focusLostTime = 0f;
    private Coroutine focusCheckRoutine;
    private float maxBackgroundTime = 60f;

    private Coroutine pingCoroutine;
    private float lastPongTime;
    private float pingSendTime;
    private bool waitingForPong;
    private int missedPongs;
    private const int MAX_MISSED_PONGS = 5;
    private const float PING_INTERVAL = 2f;
    private const float PONG_TIMEOUT = 5f;
    private const float EXIT_CLEANUP_DELAY = 1f;

    #region Initialization

    private void Awake()
    {
        isInitialized = false;
        isConnected = false;
        isExiting = false;
        isDestroyed = false;
        socketSetupStarted = false;
        exitMessageSent = false;
        RaycastBlocker = RaycastBlocker != null
            ? RaycastBlocker
            : FindSceneObject("RaycastBlocker", "Raycast Blocker", "BlackScreen");
        if (RaycastBlocker == null)
        {
            Debug.LogWarning(
                "[SocketIO] No full-screen raycast blocker was assigned or found in the active scene.");
        }
    }

    private void Start()
    {
        RequestAuthToken();
    }

    private void RequestAuthToken()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (JSManager != null)
        {
            JSManager.SendCustomMessage("authToken");
        }
#else
        authToken = testToken;
        socketURL = testSocketURL;
        InitializeSocket();
#endif
    }

    void ReceiveAuthToken(string jsonData)
    {
        if (socketSetupStarted)
        {
            Debug.LogWarning("[SocketIO] Duplicate auth token ignored");
            return;
        }

        Debug.Log($"[SocketIO] Auth received");

        try
        {
            var authData = JsonUtility.FromJson<AuthTokenData>(jsonData);
            authToken = authData.cookie;
            socketURL = authData.socketURL;

            if (!string.IsNullOrEmpty(authData.nameSpace))
            {
                nameSpace = authData.nameSpace;
            }

            InitializeSocket();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SocketIO] Auth parse failed: {e.Message}");
        }
    }

    private void InitializeSocket()
    {
        if (socketSetupStarted) return;
        socketSetupStarted = true;

        // Defensive: tear down any prior manager before building a new one
        if (socketManager != null)
        {
            try
            {
                socketManager.Close();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SocketIO] Previous socket cleanup failed: {exception.Message}");
            }
            socketManager = null;
        }

        SetRaycastBlocker(true);

        SocketOptions options = new SocketOptions
        {
            AutoConnect = false,
            Reconnection = false,
            Timeout = TimeSpan.FromSeconds(3),
            ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket
        };

        options.Auth = (SocketManager manager, Socket socket) => new { token = authToken };

#if UNITY_EDITOR
        socketManager = new SocketManager(new Uri(testSocketURL), options);
#else
        socketManager = new SocketManager(new Uri(socketURL), options);
#endif

        gameSocket = string.IsNullOrEmpty(nameSpace)
            ? socketManager.Socket
            : socketManager.GetSocket("/" + nameSpace);

        gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnSocketConnected);
        gameSocket.On(SocketIOEventTypes.Disconnect, OnSocketDisconnected);
        gameSocket.On<Error>(SocketIOEventTypes.Error, OnError);

        gameSocket.On<string>("game:init", OnInitReceived);
        gameSocket.On<string>("result", OnResultReceived);
        gameSocket.On<string>("pong", OnPongReceived);
        gameSocket.On("pong", OnPongReceivedNoArgs);
        gameSocket.On<string>("AnotherDevice", OnAnotherDevice);
        gameSocket.On<string>("balance:sync", OnBalanceSyncReceived);
        gameSocket.On<string>("jackpot:sync", OnJackpotSyncReceived);

        socketManager.Open();
    }

    #endregion

    #region Socket Events

    private void OnSocketConnected(ConnectResponse resp)
    {
        if (isExiting || isDestroyed)
        {
            Debug.LogWarning("[SocketIO] Ignoring a late connection callback during shutdown.");
            isConnected = false;
            return;
        }

        Debug.Log("[SocketIO] Connected");

        isConnected = true;
        waitingForPong = false;
        missedPongs = 0;
        lastPongTime = Time.time;
        pingSendTime = Time.realtimeSinceStartup;

        if (popupManager != null)
        {
            popupManager.CloseReconnectionPopup();
        }

        StartPingRoutine();
        SendPing();
    }

    private void OnSocketDisconnected()
    {
        Debug.Log("[SocketIO] Disconnected");

        isConnected = false;
        StopPingRoutine();

        if (uiManager != null)
        {
            uiManager.UpdatePingDisplay("-- ms");
        }

        if (isDestroyed)
        {
            return;
        }

        if (isExiting)
        {
            if (popupManager != null && !popupManager.IsLoadingPopupActive())
            {
                popupManager.ShowLoadingPopup(0f);
            }
            return;
        }

        if (popupManager != null)
        {
            popupManager.ShowDisconnectionPopup();
        }

        if (gameManager != null)
        {
            gameManager.OnDisconnected();
        }
    }

    private void OnError(Error err)
    {
        string message = err != null && !string.IsNullOrWhiteSpace(err.message)
            ? err.message
            : "Unknown socket error.";

        if (isExiting || isDestroyed)
        {
            Debug.LogWarning($"[SocketIO] Ignoring socket error during intentional shutdown: {message}");
            return;
        }

        Debug.LogError($"[SocketIO] Error: {message}");

        if (gameManager != null && !gameManager.IsInitialized)
        {
            gameManager.MarkInitializationFailed();
        }

        if (message.Contains("Session expired"))
        {
            Debug.LogWarning("Session expired detected");
            OnSocketDisconnected();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (JSManager != null) JSManager.SendCustomMessage("session_expired");
#endif
        }
        else
        {
            if (popupManager != null)
            {
                popupManager.ShowServerError(message);
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            if (JSManager != null) JSManager.SendCustomMessage("error");
#endif
        }
    }

    private void OnInitReceived(string jsonData)
    {
        if (isExiting || isDestroyed)
        {
            Debug.LogWarning("[SocketIO] Ignoring late initialization data during shutdown.");
            return;
        }

        Debug.Log($"[SocketIO] Init received: {jsonData}");

        try
        {
            var initData = JsonConvert.DeserializeObject<InitData>(jsonData);
            if (initData == null)
            {
                throw new JsonException("The game:init payload was empty.");
            }

            var gameConfig = InitDataConverter.ConvertToGameConfig(initData);
            var playerData = InitDataConverter.ConvertToPlayerData(initData.player);
            var initialMatrix = GenerateRandomMatrix(gameConfig);

            gameManager.OnInitDataReceived(gameConfig, playerData, initialMatrix);
            isInitialized = gameManager.IsInitialized;

            if (isInitialized && gameConfig.jackpotData?.values != null && uiManager != null)
            {
                uiManager.UpdateJackpotDisplay(gameConfig.jackpotData.values);
            }

            SetRaycastBlocker(false);

#if UNITY_WEBGL && !UNITY_EDITOR
            if (JSManager != null)
            {
                JSManager.SendCustomMessage("OnEnter");
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"[SocketIO] Init parse failed: {e.Message}");
            gameManager.MarkInitializationFailed();
            if (popupManager != null)
            {
                popupManager.ShowServerError("Failed to parse game initialization data.");
            }
        }
    }

    private void OnResultReceived(string jsonData)
    {
        if (!jsonData.Contains("\"id\":\"ResultData\""))
        {
            return;
        }

        Debug.Log($"[SocketIO] Result received: {jsonData}");

        try
        {
            var serverResponse = JsonConvert.DeserializeObject<ServerSpinResponse>(jsonData);

            if (!serverResponse.success)
            {
                Debug.LogError("[SocketIO] Spin failed");
                gameManager?.OnSpinRequestFailed("The server rejected the spin request.");
                return;
            }

            double currentBalance = gameManager.PlayerData.balance;
            double betAmount = gameManager.CurrentBetAmount;
            GameConfig gameConfig = gameManager.GameConfig;

            SpinResult result = InitDataConverter.ConvertServerResponseToSpinResult(
                serverResponse,
                currentBalance,
                betAmount,
                gameConfig
            );
            result.winAmountDecimalPlaces = GetWinAmountDecimalPlaces(jsonData, serverResponse);

            result.playerData.currentBetIndex = gameManager.CurrentBetIndex;

            gameManager.OnSpinResultReceived(result);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SocketIO] Result parse failed: {e.Message}");
            gameManager?.OnSpinRequestFailed("Failed to read the spin result from the server.");
        }
    }

    private static int GetWinAmountDecimalPlaces(string jsonData, ServerSpinResponse serverResponse)
    {
        if (serverResponse?.payload == null)
        {
            return 0;
        }

        string amountProperty;
        double fallbackAmount;
        if (serverResponse.payload.spinWin.HasValue)
        {
            amountProperty = "totalWin";
            fallbackAmount = serverResponse.payload.totalWin;
        }
        else if (serverResponse.payload.grandTotalWin > 0d)
        {
            amountProperty = "grandTotalWin";
            fallbackAmount = serverResponse.payload.grandTotalWin;
        }
        else if (serverResponse.payload.winAmount > 0d)
        {
            amountProperty = "winAmount";
            fallbackAmount = serverResponse.payload.winAmount;
        }
        else
        {
            amountProperty = "totalWin";
            fallbackAmount = serverResponse.payload.totalWin;
        }

        try
        {
            using (StringReader stringReader = new StringReader(jsonData))
            using (JsonTextReader jsonReader = new JsonTextReader(stringReader))
            {
                jsonReader.FloatParseHandling = FloatParseHandling.Decimal;
                JObject responseObject = JObject.Load(jsonReader);
                JToken amountToken = responseObject["payload"]?[amountProperty];

                if (amountToken != null)
                {
                    decimal preciseAmount;
                    bool parsed = amountToken.Type == JTokenType.String
                        ? decimal.TryParse(
                            amountToken.Value<string>(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out preciseAmount)
                        : decimal.TryParse(
                            amountToken.ToString(Formatting.None),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out preciseAmount);

                    if (parsed)
                    {
                        return (decimal.GetBits(preciseAmount)[3] >> 16) & 0x7F;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SocketIO] Could not preserve win amount precision: {exception.Message}");
        }

        string fallbackText = fallbackAmount.ToString("0.################", CultureInfo.InvariantCulture);
        int decimalPoint = fallbackText.IndexOf('.');
        return decimalPoint < 0 ? 0 : fallbackText.Length - decimalPoint - 1;
    }

    private void OnAnotherDevice(string data)
    {
        Debug.Log("[SocketIO] Another device login");

        if (popupManager != null)
        {
            popupManager.ShowAnotherDeviceError();
        }
    }

    private void OnBalanceSyncReceived(string jsonData)
    {
        Debug.Log($"[SocketIO] Balance Sync received: {jsonData}");

        try
        {
            BalanceSyncData syncData = JsonConvert.DeserializeObject<BalanceSyncData>(jsonData);

            if (syncData != null && gameManager != null)
            {
                gameManager.UpdateBalanceFromServer(syncData.balance);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SocketIO] Balance Sync parse failed: {e.Message}");
        }
    }

    private void OnJackpotSyncReceived(string jsonData)
    {
        Debug.Log($"[SocketIO] Jackpot Sync received: {jsonData}");

        if (string.IsNullOrWhiteSpace(jsonData))
        {
            Debug.LogWarning("[SocketIO] Jackpot Sync ignored because the payload was empty.");
            return;
        }

        try
        {
            var syncData = JsonConvert.DeserializeObject<JackpotSyncData>(jsonData);

            if (syncData == null)
            {
                Debug.LogWarning("[SocketIO] Jackpot Sync ignored because the payload was null.");
                return;
            }

            if (syncData.values == null)
            {
                Debug.LogWarning("[SocketIO] Jackpot Sync ignored because it contained no values snapshot.");
                return;
            }

            uiManager?.UpdateJackpotDisplay(syncData.values);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SocketIO] Jackpot Sync parse failed: {e.Message}");
        }
    }

    #endregion

    internal void SetRaycastBlocker(bool active)
    {
        if (RaycastBlocker == null)
        {
            RaycastBlocker = FindSceneObject("RaycastBlocker", "Raycast Blocker", "BlackScreen");
        }

        if (RaycastBlocker == null)
        {
            Debug.LogWarning($"[SocketIO] Unable to {(active ? "activate" : "disable")} the raycast blocker.");
            return;
        }

        CanvasGroup blockerCanvasGroup = RaycastBlocker.GetComponent<CanvasGroup>();
        if (active)
        {
            RaycastBlocker.SetActive(true);
            RaycastBlocker.transform.SetAsLastSibling();
            if (blockerCanvasGroup != null)
            {
                blockerCanvasGroup.interactable = true;
                blockerCanvasGroup.blocksRaycasts = true;
            }
            return;
        }

        if (blockerCanvasGroup != null)
        {
            blockerCanvasGroup.interactable = false;
            blockerCanvasGroup.blocksRaycasts = false;
        }
        RaycastBlocker.SetActive(false);
    }

    #region Focus / Background Timeout
    internal void HandleFocusChange(bool focus)
    {
        hasFocus = focus;

        if (!focus)
        {
            focusLostTime = Time.time;
            if (focusCheckRoutine == null && !isExiting && !isDestroyed)
                focusCheckRoutine = StartCoroutine(FocusTimeoutCheck());
        }
        else
        {
            StopFocusCheckRoutine();
        }
    }

    private void StopFocusCheckRoutine()
    {
        if (focusCheckRoutine == null) return;

        StopCoroutine(focusCheckRoutine);
        focusCheckRoutine = null;
    }

    private IEnumerator FocusTimeoutCheck()
    {
        while (!hasFocus && !isExiting && !isDestroyed)
        {
            if (Time.time - focusLostTime >= maxBackgroundTime)
            {
                Debug.LogWarning("[SOCKET] Background timeout — closing connection");
                isConnected = false;
                StopPingRoutine();

                if (socketManager != null)
                {
                    try { socketManager.Close(); }
                    catch (Exception e) { Debug.LogWarning($"[SOCKET] Focus close error: {e.Message}"); }
                }

                if (popupManager != null)
                {
                    popupManager.ShowDisconnectionPopup();
                }

                focusCheckRoutine = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
        }

        focusCheckRoutine = null;
    }
    #endregion

    #region Ping/Pong Health Check

    private void StartPingRoutine()
    {
        if (pingCoroutine != null)
            StopCoroutine(pingCoroutine);

        pingCoroutine = StartCoroutine(PingRoutine());
    }

    private void StopPingRoutine()
    {
        if (pingCoroutine != null)
        {
            StopCoroutine(pingCoroutine);
            pingCoroutine = null;
        }
    }

    private IEnumerator PingRoutine()
    {
        while (isConnected)
        {
            yield return new WaitForSeconds(PING_INTERVAL);

            if (waitingForPong && isInitialized)
            {
                missedPongs++;

                if (missedPongs >= MAX_MISSED_PONGS)
                {
                    Debug.LogWarning("[SocketIO] Max pongs missed - disconnecting");
                    OnSocketDisconnected();
                    yield break;
                }

                if (missedPongs >= 2 && popupManager != null)
                {
                    popupManager.ShowReconnectionPopup(missedPongs, MAX_MISSED_PONGS);
                }
            }

            SendPing();
        }
    }

    private void SendPing()
    {
        if (gameSocket != null && isConnected)
        {
            pingSendTime = Time.realtimeSinceStartup;
            waitingForPong = true;
            gameSocket.Emit("ping");
        }
    }

    private void OnPongReceivedNoArgs()
    {
        OnPongReceived(string.Empty);
    }

    private void OnPongReceived(string data)
    {
        waitingForPong = false;
        lastPongTime = Time.time;

        if (pingSendTime > 0f)
        {
            float rtt = Time.realtimeSinceStartup - pingSendTime;
            int pingMs = Mathf.Max(1, Mathf.RoundToInt(rtt * 1000f));
            if (uiManager != null)
            {
                uiManager.UpdatePingDisplay(pingMs);
            }

        }

        if (missedPongs > 0)
        {
            missedPongs = 0;

            if (popupManager != null)
            {
                popupManager.CloseReconnectionPopup();
            }
        }
    }

    #endregion

    #region Spin Request

    internal void SendSpinRequest(int betIndex, bool isFreeSpin)
    {
        Debug.Log($"[SocketIO] Spin request: betIndex={betIndex}, isFreeSpin={isFreeSpin}");

        var request = new SpinRequest
        {
            type = "SPIN",
            payload = new SpinPayload
            {
                betIndex = betIndex,
                isFreeSpin = isFreeSpin
            }
        };

        string json = JsonUtility.ToJson(request);
        gameSocket.Emit("request", json);
    }


    #endregion



    #region Cleanup

    internal void CloseGame()
    {
        if (isDestroyed)
        {
            Debug.LogWarning("[SocketIO] Exit was ignored because the socket manager is being destroyed.");
            return;
        }

        if (isExiting || exitRoutine != null || exitMessageSent)
        {
            Debug.LogWarning("[SocketIO] Duplicate exit request ignored.");
            return;
        }

        isExiting = true;
        SetRaycastBlocker(true);
        exitRoutine = StartCoroutine(CloseGameRoutine());
    }

    // Retained for compatibility with any scene event or older caller.
    internal void CloseSocket()
    {
        CloseGame();
    }

    private IEnumerator CloseGameRoutine()
    {
        StopFocusCheckRoutine();
        StopPingRoutine();
        waitingForPong = false;
        missedPongs = 0;
        isConnected = false;
        isInitialized = false;

        CloseSocketConnectionSafely("intentional exit");

        if (popupManager != null)
        {
            if (!popupManager.IsLoadingPopupActive())
            {
                popupManager.ShowLoadingPopup(0f);
            }
        }
        else
        {
            Debug.LogWarning("[SocketIO] PopupManager is missing during the exit transition.");
        }

        yield return new WaitForSecondsRealtime(EXIT_CLEANUP_DELAY);

        SendExitMessageOnce();
        exitRoutine = null;
    }

    private void CloseSocketConnectionSafely(string context)
    {
        SocketManager managerToClose = socketManager;
        Socket socketToClose = gameSocket;
        socketManager = null;
        gameSocket = null;

        try
        {
            if (managerToClose != null)
            {
                // SocketManager.Close disconnects all namespaces, releases its
                // transports, and clears its socket references.
                managerToClose.Close();
            }
            else if (socketToClose != null)
            {
                socketToClose.Disconnect();
            }
            else
            {
                Debug.LogWarning($"[SocketIO] No active socket connection to close during {context}.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SocketIO] Socket cleanup failed during {context}: {exception.Message}");
        }
        finally
        {
            isConnected = false;
        }
    }

    private void SendExitMessageOnce()
    {
        if (exitMessageSent)
        {
            return;
        }

        exitMessageSent = true;
        if (JSManager == null)
        {
            Debug.LogError("[SocketIO] Platform message 'OnExit' could not be sent because JSFunctCalls is missing.");
            return;
        }

        try
        {
            JSManager.SendCustomMessage("OnExit");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SocketIO] Failed to send platform message 'OnExit': {exception.Message}");
        }
    }

    private void OnDisable()
    {
        StopPingRoutine();
        StopFocusCheckRoutine();
    }

    private void OnApplicationQuit()
    {
        isDestroyed = true;
    }

    private void OnDestroy()
    {
        isDestroyed = true;
        StopPingRoutine();
        StopFocusCheckRoutine();
        CloseSocketConnectionSafely("object destruction");
    }

    #endregion
    private List<List<int>> GenerateRandomMatrix(GameConfig gameConfig)
    {
        int rowCount = gameConfig != null && gameConfig.rowCount > 0 ? gameConfig.rowCount : 3;
        int reelCount = gameConfig != null && gameConfig.reelCount > 0 ? gameConfig.reelCount : 5;
        var symbolIds = new List<int>();
        if (gameConfig?.symbols != null)
        {
            foreach (SymbolInfo symbol in gameConfig.symbols)
            {
                if (symbol != null && !symbolIds.Contains(symbol.id))
                {
                    symbolIds.Add(symbol.id);
                }
            }
        }

        if (symbolIds.Count == 0)
        {
            int symbolCount = gameConfig != null && gameConfig.symbolCount > 0 ? gameConfig.symbolCount : 1;
            for (int symbolId = 0; symbolId < symbolCount; symbolId++)
            {
                symbolIds.Add(symbolId);
            }
        }

        var matrix = new List<List<int>>();
        for (int col = 0; col < reelCount; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                column.Add(symbolIds[UnityEngine.Random.Range(0, symbolIds.Count)]);
            }
            matrix.Add(column);
        }
        return matrix;
    }

    private static GameObject FindSceneObject(params string[] names)
    {
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid()) continue;

            foreach (string objectName in names)
            {
                if (candidate.name == objectName)
                {
                    return candidate.gameObject;
                }
            }
        }

        return null;
    }
}


[Serializable]
public class AuthTokenData
{
    public string cookie;
    public string socketURL;
    public string nameSpace;
}

[Serializable]
public class BalanceSyncData
{
    public double balance;
}
