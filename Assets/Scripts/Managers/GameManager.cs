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
    [SerializeField] private UIManager uiManager;

    [Header("Autoplay Timing")]
    [SerializeField, Min(0f)] private float autoSpinGap = 0.25f;
    [SerializeField, Min(0f)] private float autoSpinWinGap = 0.9f;

    [Header("Free Spin Feature")]
    [SerializeField, Min(1)] private int totalFreeSpins = 6;
    [SerializeField, Min(0f)] private float freeSpinGap = 0.25f;

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
    internal bool IsFreeSpinAwaitingStart { get; private set; }
    internal bool IsFreeSpinActive { get; private set; }
    internal bool IsFreeSpinAwaitingTake { get; private set; }
    internal int FreeSpinsRemaining { get; private set; }
    internal int FreeSpinTotal => Mathf.Max(0, freeSpinTotal);
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
    private bool freeSpinRoundInProgress;
    private bool viewEventsBound;
    private Coroutine autoplayDelayRoutine;
    private Coroutine freeSpinRoutine;
    private int freeSpinTotal;
    private double freeSpinStartingBalance;
    private double freeSpinInitialServerTotalWin;
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

        if (freeSpinRoutine != null)
        {
            StopCoroutine(freeSpinRoutine);
            freeSpinRoutine = null;
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

        if (IsFreeSpinActive)
        {
            ApplyFreeSpinResultProgress(result);
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
        ResetFreeSpinFeature();
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
        if (IsAutoplayActive || autoplayRoundInProgress || IsFreeSpinActive ||
            CurrentState != GameState.Spinning ||
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

    internal bool StartPendingFreeSpins()
    {
        if (!IsFreeSpinAwaitingStart || IsFreeSpinActive || !gameData.IsInitialized ||
            !IsSocketConnected || slotBehaviour == null || !slotBehaviour.CanBeginSpinPresentation)
        {
            return false;
        }

        IsFreeSpinAwaitingStart = false;
        IsFreeSpinActive = true;
        IsFreeSpinAwaitingTake = false;
        freeSpinStartingBalance = gameData.DisplayedBalance;
        freeSpinInitialServerTotalWin = Math.Max(0d, lastCompletedResult?.serverTotalRoundWin ?? 0d);
        CurrentState = GameState.FreeSpinMode;
        slotBehaviour.BeginFreeSpinWinPresentation(
            freeSpinInitialServerTotalWin,
            lastCompletedResult?.winAmountDecimalPlaces ?? -1);
        uiManager?.BeginFreeSpinPresentation(FreeSpinTotal, FreeSpinsRemaining);
        NotifyStateChanged();

        if (StartNextFreeSpinRound())
        {
            return true;
        }

        ResetFreeSpinFeature();
        return false;
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

    internal bool TakeFreeSpinWin()
    {
        if (!IsFreeSpinActive || !IsFreeSpinAwaitingTake)
        {
            return false;
        }

        double serverTotalWin = slotBehaviour != null ? slotBehaviour.FreeSpinServerTotalWin : 0d;
        double freeSpinOnlyWin = Math.Max(0d, serverTotalWin - freeSpinInitialServerTotalWin);
        double expectedBalance = Math.Max(0d, freeSpinStartingBalance + freeSpinOnlyWin);
        double authoritativeBalance = Math.Max(0d, gameData.Player?.balance ?? 0d);
        if (Math.Abs(expectedBalance - authoritativeBalance) > 0.0001d)
        {
            Debug.LogWarning(
                $"[GameManager] Free-spin balance check differed: expected {expectedBalance:0.####}, " +
                $"server returned {authoritativeBalance:0.####}. Using the server balance.");
        }

        gameData.RestoreAuthoritativeBalance();
        ResetFreeSpinFeature(false);
        return true;
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
        gameData.SynchronizeBalance(
            balance,
            !IsCurrentlySpinning && !autoplayRoundInProgress && !IsFreeSpinActive);
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

    private bool TryStartRound(bool autoplayRound, bool freeSpinRound = false)
    {
        if (!CanBeginRound(autoplayRound, freeSpinRound))
        {
            return false;
        }

        if (!freeSpinRound && !CanAffordCurrentBet)
        {
            if (autoplayRound)
            {
                StopAutoSpin();
            }

            InsufficientBalance?.Invoke();
            Debug.LogWarning("[GameManager] Spin blocked because the balance is below the selected total bet.");
            return false;
        }

        if (!slotBehaviour.BeginSpinPresentation(CurrentSpinSpeed, autoplayRound || freeSpinRound))
        {
            return false;
        }

        autoplayRoundInProgress = autoplayRound;
        freeSpinRoundInProgress = freeSpinRound;
        IsStopRequested = CurrentSpinSpeed == SpinSpeed.QuickSpin;
        CurrentState = GameState.Spinning;
        if (!freeSpinRound)
        {
            gameData.ShowOptimisticBalance(CurrentTotalBet);
        }

        if (autoplayRound && AutoplaySpinsRemaining > 0)
        {
            AutoplaySpinsRemaining--;
        }

        try
        {
            socketManager.SendSpinRequest(gameData.CurrentBetIndex, freeSpinRound);
        }
        catch (Exception exception)
        {
            slotBehaviour.FailSpinPresentation($"Unable to send the spin request: {exception.Message}");
        }

        NotifyStateChanged();
        return true;
    }

    private bool CanBeginRound(bool autoplayRound, bool freeSpinRound)
    {
        GameState requiredState = freeSpinRound ? GameState.FreeSpinMode : GameState.Idle;
        if (!gameData.IsInitialized || CurrentState != requiredState || IsCurrentlySpinning ||
            IsWaitingForLateResult || IsResultPresentationActive || slotBehaviour == null ||
            !slotBehaviour.CanBeginSpinPresentation || !IsSocketConnected ||
            gameData.Config?.availableBets == null || gameData.Config.availableBets.Count == 0)
        {
            return false;
        }

        if (freeSpinRound)
        {
            return IsFreeSpinActive && !IsFreeSpinAwaitingStart && !freeSpinRoundInProgress;
        }

        return autoplayRound
            ? IsAutoplayActive && !IsFreeSpinActive && !IsFreeSpinAwaitingStart
            : !IsAutoplayActive && !autoplayRoundInProgress && !IsFreeSpinActive && !IsFreeSpinAwaitingStart;
    }

    private void HandleRoundStopped(SpinResult result)
    {
        if (result?.playerData != null)
        {
            gameData.ApplySpinResult(result, !IsFreeSpinActive);
        }

        lastCompletedResult = result;
        IsStopRequested = false;
        CurrentState = IsResultPresentationActive ? GameState.ShowingWin : GameState.Idle;
        NotifyStateChanged();
    }

    private void HandleRequiredPresentationCompleted(SpinResult result)
    {
        autoplayRoundInProgress = false;
        freeSpinRoundInProgress = false;
        IsStopRequested = false;

        SpinResult completedResult = result ?? lastCompletedResult;
        if (IsFreeSpinActive)
        {
            CurrentState = GameState.FreeSpinMode;
            NotifyStateChanged();

            if (FreeSpinsRemaining <= 0)
            {
                StartFreeSpinCompletion();
            }
            else
            {
                float nextSpinDelay = Math.Max(0d, completedResult?.winAmount ?? 0d) > 0d
                    ? 0f
                    : freeSpinGap;
                StartFreeSpinDelay(nextSpinDelay);
            }
            return;
        }

        if (ShouldOfferFreeSpins(completedResult))
        {
            OfferFreeSpins();
            return;
        }

        CurrentState = GameState.Idle;
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

    private static bool ShouldOfferFreeSpins(SpinResult result)
    {
        return result?.freeSpinData != null && result.freeSpinData.isTriggered &&
            result.freeSpinData.spinsAwarded > 0 && !result.isFreeSpinResult;
    }

    private void OfferFreeSpins()
    {
        StopAutoSpin();
        IsFreeSpinAwaitingStart = true;
        IsFreeSpinActive = false;
        freeSpinTotal = Mathf.Max(1, totalFreeSpins);
        FreeSpinsRemaining = FreeSpinTotal;
        CurrentState = GameState.FreeSpinMode;
        uiManager?.ShowFreeSpinOffer(FreeSpinTotal);
        NotifyStateChanged();
    }

    private bool StartNextFreeSpinRound()
    {
        if (!IsFreeSpinActive || FreeSpinsRemaining <= 0)
        {
            return false;
        }

        return TryStartRound(false, true);
    }

    private void ApplyFreeSpinResultProgress(SpinResult result)
    {
        if (result == null) return;

        if (result.freeSpinData != null && result.freeSpinData.isRetrigger)
        {
            freeSpinTotal += Mathf.Max(1, totalFreeSpins);
        }

        FreeSpinsRemaining = Mathf.Max(0, result.serverSpinsRemaining);
        uiManager?.UpdateFreeSpinCounter(FreeSpinTotal, FreeSpinsRemaining);
        NotifyStateChanged();
    }

    private void StartFreeSpinDelay(float delay)
    {
        if (freeSpinRoutine != null)
        {
            StopCoroutine(freeSpinRoutine);
        }

        freeSpinRoutine = StartCoroutine(StartNextFreeSpinAfterDelay(delay));
    }

    private IEnumerator StartNextFreeSpinAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        freeSpinRoutine = null;
        if (IsFreeSpinActive && !StartNextFreeSpinRound())
        {
            HandlePresentationFailed("The next free spin could not be started.");
        }
    }

    private void StartFreeSpinCompletion()
    {
        if (freeSpinRoutine != null)
        {
            StopCoroutine(freeSpinRoutine);
        }

        freeSpinRoutine = null;
        IsFreeSpinAwaitingTake = true;
        CurrentState = GameState.FreeSpinMode;
        uiManager?.ShowFreeSpinCompletion();
        NotifyStateChanged();
    }

    private void ResetFreeSpinFeature(bool resetWinDisplay = true)
    {
        if (freeSpinRoutine != null)
        {
            StopCoroutine(freeSpinRoutine);
            freeSpinRoutine = null;
        }

        IsFreeSpinAwaitingStart = false;
        IsFreeSpinActive = false;
        IsFreeSpinAwaitingTake = false;
        freeSpinRoundInProgress = false;
        freeSpinTotal = 0;
        freeSpinStartingBalance = 0d;
        freeSpinInitialServerTotalWin = 0d;
        FreeSpinsRemaining = 0;
        if (resetWinDisplay) gameData.RestoreAuthoritativeBalance();
        slotBehaviour?.EndFreeSpinWinPresentation(resetWinDisplay);
        uiManager?.ResetFreeSpinPresentation();

        if (gameData.IsInitialized && !IsCurrentlySpinning && !IsResultPresentationActive)
        {
            CurrentState = GameState.Idle;
        }

        NotifyStateChanged();
    }

    private void HandlePresentationFailed(string reason)
    {
        gameData.RestoreAuthoritativeBalance();
        CurrentState = gameData.IsInitialized ? GameState.Idle : GameState.Initializing;
        autoplayRoundInProgress = false;
        freeSpinRoundInProgress = false;
        IsStopRequested = false;
        StopAutoSpin();
        ResetFreeSpinFeature();
        SpinFailed?.Invoke(string.IsNullOrWhiteSpace(reason) ? "The spin request failed." : reason);
        NotifyStateChanged();
    }

    private void ResolveReferences()
    {
        socketManager = socketManager != null ? socketManager : FindSceneComponent<SocketIOManager>();
        slotBehaviour = slotBehaviour != null ? slotBehaviour : FindSceneComponent<SlotBehaviour>();
        uiManager = uiManager != null ? uiManager : FindSceneComponent<UIManager>();

        if (socketManager == null) Debug.LogError("[GameManager] SocketIOManager was not found.");
        if (slotBehaviour == null) Debug.LogError("[GameManager] SlotBehaviour was not found.");
        if (uiManager == null) Debug.LogError("[GameManager] UIManager was not found.");
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
