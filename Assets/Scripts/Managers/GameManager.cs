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
    private const double BigWinMultiplier = 5d;
    private const double SuperBigWinMultiplier = 10d;

    [Header("Controllers")]
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private SlotBehaviour slotBehaviour;
    [SerializeField] private UIManager uiManager;

    [Header("Autoplay Timing")]
    [SerializeField, Min(0f)] private float autoSpinGap = 0.25f;
    [SerializeField, Min(0f)] private float autoSpinWinGap = 0.9f;

    [Header("Free Spin Feature")]
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
        extraWinPresentationInProgress ||
        (slotBehaviour != null && slotBehaviour.IsResultPresentationActive);
    internal bool IsExtraGiftWildRevealActive =>
        slotBehaviour != null && slotBehaviour.IsExtraGiftWildRevealActive;
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
    internal bool CanChangeSpinSpeed => gameData.IsInitialized;

    private double CreditDivisor => gameData.Config != null && gameData.Config.creditDivisor > 0d
        ? gameData.Config.creditDivisor
        : 1d;

    private bool autoplayRoundInProgress;
    private bool freeSpinRoundInProgress;
    private bool viewEventsBound;
    private Coroutine autoplayDelayRoutine;
    private Coroutine freeSpinRoutine;
    private int freeSpinTotal;
    private bool freeSpinRetriggerPending;
    private int pendingFreeSpinRetriggerTotal;
    private int pendingFreeSpinRetriggerRemaining;
    private double freeSpinStartingBalance;
    private double freeSpinInitialServerTotalWin;
    private bool freeSpinBalanceDeferred;
    private SpinResult lastCompletedResult;
    private bool slotPresentationCompleted;
    private bool extraWinPresentationInProgress;
    private int extraWinPresentationVersion;

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
        CancelExtraWinPresentation();
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
        CancelExtraWinPresentation();
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
        bool resumingAfterRetrigger = freeSpinRetriggerPending;
        if (!freeSpinBalanceDeferred)
        {
            freeSpinStartingBalance = gameData.DisplayedBalance;
            freeSpinBalanceDeferred = true;
        }
        CurrentState = GameState.FreeSpinMode;
        if (!resumingAfterRetrigger)
        {
            slotBehaviour.BeginFreeSpinWinPresentation(
                freeSpinInitialServerTotalWin,
                lastCompletedResult?.winAmountDecimalPlaces ?? -1);
        }
        NotifyStateChanged();

        int presentationTotal = resumingAfterRetrigger
            ? pendingFreeSpinRetriggerTotal
            : FreeSpinTotal;
        int presentationRemaining = resumingAfterRetrigger
            ? pendingFreeSpinRetriggerRemaining
            : FreeSpinsRemaining;
        Action onPanelHidden = resumingAfterRetrigger
            ? CompleteFreeSpinRetriggerPresentation
            : BeginFreeSpinStartReelTransition;

        if (uiManager != null)
        {
            uiManager.BeginFreeSpinPresentation(
                presentationTotal,
                presentationRemaining,
                onPanelHidden);
            return true;
        }

        onPanelHidden();
        return true;
    }

    private void CompleteFreeSpinRetriggerPresentation()
    {
        if (!IsFreeSpinActive || !freeSpinRetriggerPending)
        {
            return;
        }

        freeSpinTotal = pendingFreeSpinRetriggerTotal;
        FreeSpinsRemaining = pendingFreeSpinRetriggerRemaining;
        ClearPendingFreeSpinRetrigger();
        uiManager?.UpdateFreeSpinCounter(FreeSpinTotal, FreeSpinsRemaining);
        NotifyStateChanged();
        BeginFreeSpinStartReelTransition();
    }

    private void BeginFreeSpinStartReelTransition()
    {
        if (!IsFreeSpinActive)
        {
            return;
        }

        if (slotBehaviour == null ||
            !slotBehaviour.PlayFreeSpinStartSymbolTransition(StartFirstFreeSpinAfterTransition))
        {
            HandlePresentationFailed("The Free Games reel transition could not be started.");
        }
    }

    private void StartFirstFreeSpinAfterTransition()
    {
        if (!IsFreeSpinActive)
        {
            return;
        }

        if (!StartNextFreeSpinRound())
        {
            HandlePresentationFailed("The first Free Game could not be started.");
        }
    }

    internal bool StartAutoSpin(int spinCount)
    {
        string blockReason = GetAutoplayStartBlockReason(spinCount);
        if (!string.IsNullOrEmpty(blockReason))
        {
            Debug.LogWarning($"[GameManager] Autoplay could not start: {blockReason}");
            return false;
        }

        IsAutoplayActive = true;
        AutoplaySpinsRemaining = spinCount < 0 ? InfiniteAutoplay : spinCount;
        NotifyStateChanged();

        if (TryStartRound(true))
        {
            return true;
        }

        Debug.LogWarning("[GameManager] Autoplay could not start because the first round became unavailable.");
        StopAutoSpin();
        return false;
    }

    internal string GetAutoplayStartBlockReason(int spinCount)
    {
        if (spinCount == 0 || spinCount < InfiniteAutoplay)
        {
            return "the selected spin count is invalid.";
        }

        if (IsAutoplayActive)
        {
            return "autoplay is already active.";
        }

        if (!gameData.IsInitialized)
        {
            return "the game has not finished initializing.";
        }

        if (!IsSocketConnected)
        {
            return "the game is not connected to the server.";
        }

        if (!CanAffordCurrentBet)
        {
            return "the balance is below the selected total bet.";
        }

        if (CurrentState != GameState.Idle || IsCurrentlySpinning || autoplayRoundInProgress)
        {
            return "another spin is already in progress.";
        }

        if (IsWaitingForLateResult || IsResultPresentationActive)
        {
            return "the previous spin presentation has not finished.";
        }

        if (IsFreeSpinActive || IsFreeSpinAwaitingStart)
        {
            return "Free Games are active or awaiting confirmation.";
        }

        if (slotBehaviour == null || !slotBehaviour.CanBeginSpinPresentation)
        {
            return "the reel presentation is not ready.";
        }

        if (gameData.Config?.availableBets == null || gameData.Config.availableBets.Count == 0)
        {
            return "no bet configuration is available.";
        }

        return null;
    }

    internal bool TakeFreeSpinWin()
    {
        if (!IsFreeSpinActive || !IsFreeSpinAwaitingTake)
        {
            return false;
        }

        double serverTotalWin = slotBehaviour != null ? slotBehaviour.FreeSpinServerTotalWin : 0d;
        double totalFreeGamesWin = Math.Max(0d, serverTotalWin);
        double expectedBalance = Math.Max(0d, freeSpinStartingBalance + totalFreeGamesWin);
        double authoritativeBalance = Math.Max(0d, gameData.Player?.balance ?? 0d);
        if (Math.Abs(expectedBalance - authoritativeBalance) > 0.0001d)
        {
            Debug.LogWarning(
                $"[GameManager] Free-spin balance check differed: expected {expectedBalance:0.####}, " +
                $"server returned {authoritativeBalance:0.####}. Using the server balance.");
        }

        IsFreeSpinAwaitingTake = false;
        NotifyStateChanged();

        if (uiManager != null && uiManager.PlayFreeSpinCollectTransition(CompleteFreeSpinCollection))
        {
            return true;
        }

        CompleteFreeSpinCollection();
        return true;
    }

    private void CompleteFreeSpinCollection()
    {
        gameData.RestoreAuthoritativeBalance();
        ResetFreeSpinFeature(false, false);
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
        if (!CanChangeSpinSpeed)
        {
            return false;
        }

        CurrentSpinSpeed = CurrentSpinSpeed == SpinSpeed.Normal
            ? SpinSpeed.Turbo
            : CurrentSpinSpeed == SpinSpeed.Turbo
                ? SpinSpeed.QuickSpin
                : SpinSpeed.Normal;

        bool canApplyToCurrentSpin = CurrentState == GameState.Spinning && !IsStopRequested &&
            slotBehaviour != null && slotBehaviour.IsCurrentlySpinning;
        if (canApplyToCurrentSpin)
        {
            slotBehaviour.ApplySpinSpeed(CurrentSpinSpeed);
            if (CurrentSpinSpeed == SpinSpeed.QuickSpin)
            {
                IsStopRequested = true;
                CurrentState = GameState.Stopping;
            }
        }

        NotifyStateChanged();
        return true;
    }

    internal void UpdateBalanceFromServer(double balance)
    {
        gameData.SynchronizeBalance(balance, true);
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
        if (socketManager == null)
        {
            Debug.LogError("[GameManager] Exit could not continue because SocketIOManager is missing.");
            return;
        }

        socketManager.CloseGame();
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

        slotPresentationCompleted = false;
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
            if (freeSpinRound)
            {
                uiManager?.UpdateFreeSpinCounter(
                    FreeSpinTotal,
                    Mathf.Max(0, FreeSpinsRemaining - 1));
            }
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
        bool triggersFreeSpins = ShouldOfferFreeSpins(result);
        if (triggersFreeSpins && !freeSpinBalanceDeferred)
        {
            freeSpinStartingBalance = gameData.DisplayedBalance;
            freeSpinInitialServerTotalWin = Math.Max(
                Math.Max(0d, result?.serverTotalRoundWin ?? 0d),
                Math.Max(0d, result?.winAmount ?? 0d));
            freeSpinBalanceDeferred = true;
        }

        if (result?.playerData != null)
        {
            gameData.ApplySpinResult(result, !freeSpinBalanceDeferred);
        }

        lastCompletedResult = result;
        TryStartExtraWinPresentation(result);
        if (IsFreeSpinActive)
        {
            if (ShouldOfferFreeSpinRetrigger(result))
            {
                QueueFreeSpinRetrigger(result);
            }
            else
            {
                ApplyFreeSpinResultProgress(result);
            }
        }
        IsStopRequested = false;
        CurrentState = IsResultPresentationActive ? GameState.ShowingWin : GameState.Idle;
        NotifyStateChanged();
    }

    private void HandleRequiredPresentationCompleted(SpinResult result)
    {
        SpinResult completedResult = result ?? lastCompletedResult;
        slotPresentationCompleted = true;
        if (extraWinPresentationInProgress)
        {
            CurrentState = GameState.ShowingWin;
            NotifyStateChanged();
            return;
        }

        CompleteRoundPresentation(completedResult);
    }

    private void CompleteRoundPresentation(SpinResult completedResult)
    {
        slotPresentationCompleted = false;
        autoplayRoundInProgress = false;
        freeSpinRoundInProgress = false;
        IsStopRequested = false;

        if (IsFreeSpinActive)
        {
            CurrentState = GameState.FreeSpinMode;
            NotifyStateChanged();

            if (freeSpinRetriggerPending)
            {
                OfferFreeSpinRetrigger();
                return;
            }

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
            OfferFreeSpins(completedResult);
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

        float delay = GetWinAmount(completedResult) > 0d ? autoSpinWinGap : autoSpinGap;
        if (autoplayDelayRoutine != null)
        {
            StopCoroutine(autoplayDelayRoutine);
        }

        autoplayDelayRoutine = StartCoroutine(StartNextAutoplayRound(delay));
    }

    private void TryStartExtraWinPresentation(SpinResult result)
    {
        if (result == null || result.isFreeSpinResult || IsFreeSpinActive || uiManager == null)
        {
            return;
        }

        double totalBet = CurrentTotalBet;
        double winAmount = result.winAmount > 0d ? result.winAmount : result.grandTotalWin;
        if (totalBet <= 0d || winAmount <= 0d)
        {
            return;
        }

        double calculatedMultiplier = winAmount / totalBet;
        WinPopupType popupType = calculatedMultiplier >= SuperBigWinMultiplier
            ? WinPopupType.SuperBigWin
            : calculatedMultiplier >= BigWinMultiplier
                ? WinPopupType.BigWin
                : WinPopupType.RegularWin;
        if (popupType == WinPopupType.RegularWin)
        {
            return;
        }

        int presentationVersion = ++extraWinPresentationVersion;
        extraWinPresentationInProgress = true;
        bool started = uiManager.ShowExtraWinPresentation(
            popupType,
            winAmount,
            result.winAmountDecimalPlaces,
            () => HandleExtraWinPresentationCompleted(presentationVersion));
        if (!started)
        {
            extraWinPresentationInProgress = false;
            return;
        }

        Debug.Log(
            $"[GameManager] {popupType} selected: win {winAmount:0.####} / " +
            $"total bet {totalBet:0.####} = {calculatedMultiplier:0.####}x " +
            $"(server payload totalMultiplier: {result.totalMultiplier:0.####}).");
    }

    private void HandleExtraWinPresentationCompleted(int presentationVersion)
    {
        if (presentationVersion != extraWinPresentationVersion)
        {
            return;
        }

        extraWinPresentationInProgress = false;
        if (slotPresentationCompleted)
        {
            CompleteRoundPresentation(lastCompletedResult);
            return;
        }

        CurrentState = slotBehaviour != null && slotBehaviour.IsResultPresentationActive
            ? GameState.ShowingWin
            : CurrentState;
        NotifyStateChanged();
    }

    private void CancelExtraWinPresentation()
    {
        extraWinPresentationVersion++;
        extraWinPresentationInProgress = false;
        slotPresentationCompleted = false;
        uiManager?.HideExtraWinPresentation();
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

    private static bool ShouldOfferFreeSpinRetrigger(SpinResult result)
    {
        return result?.freeSpinData != null && result.freeSpinData.isTriggered &&
            result.freeSpinData.isRetrigger && result.freeSpinData.spinsAwarded > 0 &&
            result.isFreeSpinResult;
    }

    private void OfferFreeSpins(SpinResult triggerResult)
    {
        int awardedSpins = Mathf.Max(0, triggerResult?.freeSpinData?.spinsAwarded ?? 0);
        int serverTotal = Mathf.Max(0, triggerResult?.serverTotalSpins ?? 0);
        int serverRemaining = Mathf.Max(0, triggerResult?.serverSpinsRemaining ?? 0);

        StopAutoSpin();
        IsFreeSpinAwaitingStart = true;
        IsFreeSpinActive = false;
        freeSpinTotal = serverTotal > 0 ? serverTotal : awardedSpins;
        FreeSpinsRemaining = serverRemaining > 0
            ? serverRemaining
            : Mathf.Max(0, triggerResult?.freeSpinData?.remainingSpins ?? freeSpinTotal);
        CurrentState = GameState.FreeSpinMode;
        uiManager?.ShowFreeSpinOffer(FreeSpinTotal, FreeSpinsRemaining);
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

        CalculateFreeSpinResultProgress(result, out int totalSpins, out int remainingSpins);
        freeSpinTotal = totalSpins;
        FreeSpinsRemaining = remainingSpins;
        uiManager?.UpdateFreeSpinCounter(FreeSpinTotal, FreeSpinsRemaining);
    }

    private void QueueFreeSpinRetrigger(SpinResult result)
    {
        CalculateFreeSpinResultProgress(
            result,
            out pendingFreeSpinRetriggerTotal,
            out pendingFreeSpinRetriggerRemaining);
        freeSpinRetriggerPending = true;
    }

    private void OfferFreeSpinRetrigger()
    {
        IsFreeSpinAwaitingStart = true;
        IsFreeSpinActive = false;
        IsFreeSpinAwaitingTake = false;
        CurrentState = GameState.FreeSpinMode;
        uiManager?.ShowFreeSpinOffer(
            pendingFreeSpinRetriggerTotal,
            pendingFreeSpinRetriggerRemaining);
        NotifyStateChanged();
    }

    private void CalculateFreeSpinResultProgress(
        SpinResult result,
        out int totalSpins,
        out int remainingSpins)
    {
        totalSpins = freeSpinTotal;
        remainingSpins = FreeSpinsRemaining;
        if (result == null)
        {
            return;
        }

        int serverTotal = Mathf.Max(0, result.serverTotalSpins);
        if (serverTotal > 0)
        {
            totalSpins = serverTotal;
        }
        else if (result.freeSpinData != null && result.freeSpinData.isRetrigger)
        {
            totalSpins += Mathf.Max(0, result.freeSpinData.spinsAwarded);
        }

        remainingSpins = Mathf.Max(0, result.serverSpinsRemaining);
    }

    private void ClearPendingFreeSpinRetrigger()
    {
        freeSpinRetriggerPending = false;
        pendingFreeSpinRetriggerTotal = 0;
        pendingFreeSpinRetriggerRemaining = 0;
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

        double serverTotalWin = slotBehaviour != null ? slotBehaviour.FreeSpinServerTotalWin : 0d;
        double totalFreeGamesWin = Math.Max(0d, serverTotalWin);
        string formattedFreeSpinWin = slotBehaviour != null
            ? slotBehaviour.ShowFreeSpinCompletionWin(totalFreeGamesWin)
            : "0";

        uiManager?.ShowFreeSpinCompletion(formattedFreeSpinWin);
        NotifyStateChanged();
    }

    private void ResetFreeSpinFeature(bool resetWinDisplay = true, bool resetUiPresentation = true)
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
        ClearPendingFreeSpinRetrigger();
        freeSpinStartingBalance = 0d;
        freeSpinInitialServerTotalWin = 0d;
        freeSpinBalanceDeferred = false;
        FreeSpinsRemaining = 0;
        if (resetWinDisplay) gameData.RestoreAuthoritativeBalance();
        slotBehaviour?.CancelFreeSpinStartSymbolTransition();
        slotBehaviour?.EndFreeSpinWinPresentation(resetWinDisplay);
        if (resetUiPresentation) uiManager?.ResetFreeSpinPresentation();

        if (gameData.IsInitialized && !IsCurrentlySpinning && !IsResultPresentationActive)
        {
            CurrentState = GameState.Idle;
        }

        NotifyStateChanged();
    }

    private void HandlePresentationFailed(string reason)
    {
        CancelExtraWinPresentation();
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
        slotBehaviour.SpinControlPresentationChanged += NotifyStateChanged;
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
        slotBehaviour.SpinControlPresentationChanged -= NotifyStateChanged;
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
