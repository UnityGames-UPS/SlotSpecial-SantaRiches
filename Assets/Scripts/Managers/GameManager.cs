using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns gameplay commands and reads authoritative runtime values from
/// GameRuntimeData.
/// SocketIOManager is transport-only and SlotBehaviour is presentation-only.
/// </summary>
[DefaultExecutionOrder(-11000)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    private const int InfiniteAutoplay = -1;

    [Header("Controllers")]
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private SlotBehaviour slotBehaviour;

    [Header("Autoplay Timing")]
    [SerializeField, Min(0f)] private float autoSpinGap = 0.25f;
    [SerializeField, Min(0f)] private float autoSpinWinGap = 0.9f;

    private readonly GameRuntimeData gameData = new GameRuntimeData();

    internal event Action<GameConfig, PlayerData, List<List<int>>> InitDataReceived;
    internal event Action<SpinResult> SpinResultReceived;
    internal event Action Disconnected;
    internal event Action StateChanged;
    internal event Action InsufficientBalance;
    internal event Action<string> SpinFailed;

    internal bool IsInitialized => gameData.IsInitialized;
    internal bool InitializationFailed => gameData.InitializationFailed;
    internal PlayerData PlayerData => gameData.Player;
    internal GameConfig GameConfig => gameData.Config;
    internal int CurrentBetIndex => gameData.CurrentBetIndex;
    internal double CurrentBetAmount => gameData.CurrentBetAmount;
    internal GameState CurrentState { get; private set; } = GameState.Initializing;
    internal SpinSpeed CurrentSpinSpeed { get; private set; } = SpinSpeed.Normal;
    internal bool IsAutoplayActive { get; private set; }
    internal int AutoplaySpinsRemaining { get; private set; }
    internal bool IsStopRequested { get; private set; }
    internal double CurrentBalance => gameData.DisplayedBalance;
    internal double CurrentTotalBet => gameData.CurrentBetAmount * CreditDivisor;
    internal int AvailableBetCount => gameData.Config?.availableBets?.Count ?? 0;
    internal bool IsMaximumBet => AvailableBetCount > 0 && gameData.CurrentBetIndex == AvailableBetCount - 1;
    internal bool IsSocketConnected => socketManager != null && socketManager.isConnected;
    internal bool IsCurrentlySpinning =>
        CurrentState == GameState.Spinning || CurrentState == GameState.Stopping ||
        (slotBehaviour != null && slotBehaviour.IsCurrentlySpinning);
    internal bool IsWaitingForLateResult => slotBehaviour != null && slotBehaviour.IsWaitingForLateResult;
    internal bool IsResultPresentationActive =>
        slotBehaviour != null && slotBehaviour.IsResultPresentationActive;
    internal bool IsAutoplayRoundSettling => autoplayRoundInProgress && !IsAutoplayActive &&
        (IsCurrentlySpinning || IsWaitingForLateResult || IsResultPresentationActive);
    internal bool CanAffordCurrentBet => gameData.DisplayedBalance + 0.0000001d >= CurrentTotalBet;
    internal bool CanAttemptManualSpin => gameData.IsInitialized && !IsAutoplayActive && !autoplayRoundInProgress &&
        CurrentState == GameState.Idle && !IsCurrentlySpinning && !IsWaitingForLateResult &&
        !IsResultPresentationActive && IsSocketConnected;
    internal bool CanStartManualSpin => CanAttemptManualSpin && CanAffordCurrentBet;
    internal bool CanChangeBet => gameData.IsInitialized && !IsAutoplayActive && !autoplayRoundInProgress &&
        CurrentState == GameState.Idle && !IsCurrentlySpinning && !IsWaitingForLateResult &&
        !IsResultPresentationActive;

    private double CreditDivisor => gameData.Config != null && gameData.Config.creditDivisor > 0d
        ? gameData.Config.creditDivisor
        : 1d;

    private bool autoplayRoundInProgress;
    private bool viewEventsBound;
    private Coroutine autoplayDelayRoutine;
    private SpinResult lastCompletedResult;

    private void Awake()
    {
        ResolveReferences();
        BindViewEvents();
    }

    private void OnEnable()
    {
        BindViewEvents();
    }

    private void OnDisable()
    {
        UnbindViewEvents();
        if (autoplayDelayRoutine != null)
        {
            StopCoroutine(autoplayDelayRoutine);
            autoplayDelayRoutine = null;
        }
    }

    internal void OnInitDataReceived(
        GameConfig config,
        PlayerData initialPlayerData,
        List<List<int>> initialMatrix)
    {
        if (gameData.ApplyInitialization(config, initialPlayerData))
        {
            CurrentState = GameState.Idle;
        }

        InitDataReceived?.Invoke(config, initialPlayerData, initialMatrix);
        NotifyStateChanged();
    }

    internal void OnSpinResultReceived(SpinResult result)
    {
        if (result == null)
        {
            OnSpinRequestFailed("The server returned an empty spin result.");
            return;
        }

        if (result.playerData != null)
        {
            result.playerData.currentBetIndex = gameData.CurrentBetIndex;
        }

        SpinResultReceived?.Invoke(result);
    }

    internal void OnSpinRequestFailed(string reason)
    {
        string message = string.IsNullOrWhiteSpace(reason) ? "The spin request failed." : reason;
        if (slotBehaviour != null && (slotBehaviour.IsCurrentlySpinning || slotBehaviour.IsWaitingForLateResult))
        {
            slotBehaviour.FailSpinPresentation(message);
            return;
        }

        HandlePresentationFailed(message);
    }

    internal void OnDisconnected()
    {
        StopAutoSpin();
        Disconnected?.Invoke();

        if (!IsCurrentlySpinning && !IsResultPresentationActive)
        {
            CurrentState = gameData.IsInitialized ? GameState.Idle : GameState.Initializing;
        }

        NotifyStateChanged();
    }

    internal bool TryStartManualSpin()
    {
        return TryStartRound(false);
    }

    internal bool RequestStopSpin()
    {
        if (IsAutoplayActive || autoplayRoundInProgress || CurrentState != GameState.Spinning ||
            IsStopRequested || slotBehaviour == null)
        {
            return false;
        }

        if (!slotBehaviour.RequestStopPresentation())
        {
            return false;
        }

        IsStopRequested = true;
        CurrentState = GameState.Stopping;
        NotifyStateChanged();
        return true;
    }

    internal bool StartAutoSpin(int spinCount)
    {
        if (spinCount == 0 || spinCount < InfiniteAutoplay || IsAutoplayActive || !CanStartManualSpin)
        {
            return false;
        }

        IsAutoplayActive = true;
        AutoplaySpinsRemaining = spinCount < 0 ? InfiniteAutoplay : spinCount;
        NotifyStateChanged();

        if (TryStartRound(true))
        {
            return true;
        }

        StopAutoSpin();
        return false;
    }

    internal void StopAutoSpin()
    {
        if (autoplayDelayRoutine != null)
        {
            StopCoroutine(autoplayDelayRoutine);
            autoplayDelayRoutine = null;
        }

        if (!IsAutoplayActive && AutoplaySpinsRemaining == 0)
        {
            return;
        }

        IsAutoplayActive = false;
        AutoplaySpinsRemaining = 0;
        NotifyStateChanged();
    }

    internal bool TryChangeBet(bool increase)
    {
        if (!CanChangeBet || gameData.Config?.availableBets == null || gameData.Config.availableBets.Count == 0)
        {
            return false;
        }

        int direction = increase ? 1 : -1;
        int nextBetIndex = (gameData.CurrentBetIndex + direction + gameData.Config.availableBets.Count) %
            gameData.Config.availableBets.Count;
        if (!gameData.SelectBet(nextBetIndex))
        {
            return false;
        }

        slotBehaviour?.OnBetChanged();
        NotifyStateChanged();
        return true;
    }

    internal bool CycleSpinSpeed()
    {
        if (!CanChangeBet)
        {
            return false;
        }

        CurrentSpinSpeed = CurrentSpinSpeed == SpinSpeed.Normal
            ? SpinSpeed.Turbo
            : CurrentSpinSpeed == SpinSpeed.Turbo
                ? SpinSpeed.QuickSpin
                : SpinSpeed.Normal;
        NotifyStateChanged();
        return true;
    }

    internal void UpdateBalanceFromServer(double balance)
    {
        gameData.SynchronizeBalance(balance, !IsCurrentlySpinning && !autoplayRoundInProgress);
        NotifyStateChanged();
    }

    internal void MarkInitializationFailed()
    {
        gameData.MarkInitializationFailed();
        CurrentState = GameState.Initializing;
        NotifyStateChanged();
    }

    internal void ExitGame()
    {
        StopAutoSpin();
        socketManager?.CloseSocket();
    }

    private bool TryStartRound(bool autoplayRound)
    {
        if (!CanBeginRound(autoplayRound))
        {
            return false;
        }

        if (!CanAffordCurrentBet)
        {
            if (autoplayRound)
            {
                StopAutoSpin();
            }

            InsufficientBalance?.Invoke();
            Debug.LogWarning("[GameManager] Spin blocked because the balance is below the selected total bet.");
            return false;
        }

        if (!slotBehaviour.BeginSpinPresentation(CurrentSpinSpeed, autoplayRound))
        {
            return false;
        }

        autoplayRoundInProgress = autoplayRound;
        IsStopRequested = CurrentSpinSpeed == SpinSpeed.QuickSpin;
        CurrentState = GameState.Spinning;
        gameData.ShowOptimisticBalance(CurrentTotalBet);

        if (autoplayRound && AutoplaySpinsRemaining > 0)
        {
            AutoplaySpinsRemaining--;
        }

        try
        {
            socketManager.SendSpinRequest(gameData.CurrentBetIndex, false);
        }
        catch (Exception exception)
        {
            slotBehaviour.FailSpinPresentation($"Unable to send the spin request: {exception.Message}");
        }

        NotifyStateChanged();
        return true;
    }

    private bool CanBeginRound(bool autoplayRound)
    {
        if (!gameData.IsInitialized || CurrentState != GameState.Idle || IsCurrentlySpinning ||
            IsWaitingForLateResult || IsResultPresentationActive || slotBehaviour == null ||
            !slotBehaviour.CanBeginSpinPresentation || !IsSocketConnected ||
            gameData.Config?.availableBets == null || gameData.Config.availableBets.Count == 0)
        {
            return false;
        }

        return autoplayRound ? IsAutoplayActive : !IsAutoplayActive && !autoplayRoundInProgress;
    }

    private void HandleRoundStopped(SpinResult result)
    {
        if (result?.playerData != null)
        {
            gameData.ApplySpinResult(result);
        }

        lastCompletedResult = result;
        IsStopRequested = false;
        CurrentState = IsResultPresentationActive ? GameState.ShowingWin : GameState.Idle;
        NotifyStateChanged();
    }

    private void HandleRequiredPresentationCompleted(SpinResult result)
    {
        CurrentState = GameState.Idle;
        autoplayRoundInProgress = false;
        IsStopRequested = false;
        NotifyStateChanged();

        if (!IsAutoplayActive)
        {
            return;
        }

        if (AutoplaySpinsRemaining == 0)
        {
            StopAutoSpin();
            return;
        }

        if (!CanAffordCurrentBet)
        {
            StopAutoSpin();
            InsufficientBalance?.Invoke();
            return;
        }

        float delay = GetWinAmount(result ?? lastCompletedResult) > 0d ? autoSpinWinGap : autoSpinGap;
        if (autoplayDelayRoutine != null)
        {
            StopCoroutine(autoplayDelayRoutine);
        }

        autoplayDelayRoutine = StartCoroutine(StartNextAutoplayRound(delay));
    }

    private IEnumerator StartNextAutoplayRound(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        autoplayDelayRoutine = null;
        if (IsAutoplayActive && !TryStartRound(true))
        {
            StopAutoSpin();
        }
    }

    private void HandlePresentationFailed(string reason)
    {
        gameData.RestoreAuthoritativeBalance();
        CurrentState = gameData.IsInitialized ? GameState.Idle : GameState.Initializing;
        autoplayRoundInProgress = false;
        IsStopRequested = false;
        StopAutoSpin();
        SpinFailed?.Invoke(string.IsNullOrWhiteSpace(reason) ? "The spin request failed." : reason);
        NotifyStateChanged();
    }

    private void ResolveReferences()
    {
        socketManager = socketManager != null ? socketManager : FindSceneComponent<SocketIOManager>();
        slotBehaviour = slotBehaviour != null ? slotBehaviour : FindSceneComponent<SlotBehaviour>();

        if (socketManager == null) Debug.LogError("[GameManager] SocketIOManager was not found.");
        if (slotBehaviour == null) Debug.LogError("[GameManager] SlotBehaviour was not found.");
    }

    private void BindViewEvents()
    {
        if (viewEventsBound)
        {
            return;
        }

        slotBehaviour = slotBehaviour != null ? slotBehaviour : FindSceneComponent<SlotBehaviour>();
        if (slotBehaviour == null)
        {
            return;
        }

        slotBehaviour.RoundStopped += HandleRoundStopped;
        slotBehaviour.RequiredPresentationCompleted += HandleRequiredPresentationCompleted;
        slotBehaviour.PresentationFailed += HandlePresentationFailed;
        viewEventsBound = true;
    }

    private void UnbindViewEvents()
    {
        if (!viewEventsBound || slotBehaviour == null)
        {
            return;
        }

        slotBehaviour.RoundStopped -= HandleRoundStopped;
        slotBehaviour.RequiredPresentationCompleted -= HandleRequiredPresentationCompleted;
        slotBehaviour.PresentationFailed -= HandlePresentationFailed;
        viewEventsBound = false;
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private static double GetWinAmount(SpinResult result)
    {
        return result == null ? 0d : result.grandTotalWin > 0d ? result.grandTotalWin : result.winAmount;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(candidate => candidate != null && candidate.gameObject.scene.IsValid());
    }
}
