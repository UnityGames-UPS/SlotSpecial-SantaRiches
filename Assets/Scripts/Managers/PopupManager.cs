using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PopupManager : MonoBehaviour
{
    private enum PopupKind
    {
        None,
        Loading,
        Disconnection,
        ServerError,
        AnotherDevice,
        Reconnection,
        InsufficientBalance,
        ExitConfirmation
    }

    [Header("Shared Popup Presentation")]
    [SerializeField] private GameObject popupParent;
    [SerializeField] private GameObject genericMessagePopup;
    [SerializeField] private RectTransform genericMessagePopupRect;
    [SerializeField] private TMP_Text genericTitleText;
    [SerializeField] private TMP_Text genericMessageText;
    [SerializeField] private Button genericOkayButton;
    [SerializeField] private TMP_Text genericOkayButtonText;

    [Header("Optional Existing Popup Roots")]
    [SerializeField] private GameObject loadingPopup;
    [SerializeField] private GameObject disconnectionPopup;
    [SerializeField] private GameObject serverErrorPopup;
    [SerializeField] private GameObject anotherDevicePopup;
    [SerializeField] private GameObject reconnectionPopup;
    [SerializeField] private GameObject insufficientBalancePopup;
    [SerializeField] private GameObject exitConfirmationPopup;

    [Header("Optional Popup Rect Transforms")]
    [SerializeField] private RectTransform loadingPopupRect;
    [SerializeField] private RectTransform disconnectionPopupRect;
    [SerializeField] private RectTransform serverErrorPopupRect;
    [SerializeField] private RectTransform anotherDevicePopupRect;
    [SerializeField] private RectTransform reconnectionPopupRect;
    [SerializeField] private RectTransform insufficientBalancePopupRect;
    [SerializeField] private RectTransform exitConfirmationPopupRect;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text disconnectionMessageText;
    [SerializeField] private TMP_Text serverErrorText;
    [SerializeField] private TMP_Text reconnectionText;
    [SerializeField] private TMP_Text loadingAnimatedText;

    [Header("Optional Busy Visuals")]
    [SerializeField] private Image loadingRotatingImage;
    [SerializeField] private Image reconnectionRotatingImage;

    [Header("Optional Exit Confirmation Buttons")]
    [SerializeField] private Button confirmExitButton;
    [SerializeField] private Button cancelExitButton;

    [Header("Animation Settings")]
    [SerializeField, Min(0f)] private float popupScaleInDuration = 0.3f;
    [SerializeField, Min(0f)] private float popupScaleOutDuration = 0.2f;
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;
    [SerializeField, Min(0.05f)] private float loadingTextCycleSpeed = 0.5f;
    [SerializeField, Min(0f)] private float defaultLoadingDuration = 5f;
    [SerializeField, Min(0f)] private float minLoadingDuration = 0.8f;

    [Header("Optional References")]
    [SerializeField] private AudioManager audioManager;

    internal event Action BlockingStateChanged;
    internal event Action ExitConfirmed;

    private GameObject currentActivePopup;
    private RectTransform currentActivePopupRect;
    private PopupKind currentPopupKind;
    private Coroutine rotationRoutine;
    private Coroutine loadingTextRoutine;
    private Coroutine loadingRoutine;
    private Coroutine delayedLoadingCloseRoutine;
    private TMP_Text activeLoadingText;
    private string activeLoadingTextPrefix = "Loading";
    private float loadingStartTime;
    private Action onLoadingClosed;
    private bool genericOkayExits;
    private bool exitConfirmationClosing;
    private bool exitConfirmed;

    internal bool IsBlockingPopupActive => IsActive(genericMessagePopup) ||
        IsActive(loadingPopup) || IsActive(disconnectionPopup) || IsActive(serverErrorPopup) ||
        IsActive(anotherDevicePopup) || IsActive(reconnectionPopup) ||
        IsActive(insufficientBalancePopup) || IsActive(exitConfirmationPopup);

    private void Awake()
    {
        ResolveReferences();
        HideAllPopupsImmediately();
    }

    private void OnEnable()
    {
        if (genericOkayButton != null) genericOkayButton.onClick.AddListener(OnGenericOkayClicked);
        if (confirmExitButton != null) confirmExitButton.onClick.AddListener(ConfirmExit);
        if (cancelExitButton != null) cancelExitButton.onClick.AddListener(CancelExit);
    }

    private void OnDisable()
    {
        if (genericOkayButton != null) genericOkayButton.onClick.RemoveListener(OnGenericOkayClicked);
        if (confirmExitButton != null) confirmExitButton.onClick.RemoveListener(ConfirmExit);
        if (cancelExitButton != null) cancelExitButton.onClick.RemoveListener(CancelExit);
        StopBusyAnimations();
        StopLoadingTimers();
        KillPopupTweens();
    }

    internal void ShowDisconnectionPopup()
    {
        ShowDisconnectionPopup("Game disconnected due to network error. Please relaunch the game.");
    }

    internal void ShowDisconnectionPopup(string message)
    {
        GameObject popup = disconnectionPopup != null ? disconnectionPopup : genericMessagePopup;
        RectTransform rect = disconnectionPopup != null ? disconnectionPopupRect : genericMessagePopupRect;
        if (popup == null) return;

        if (popup == genericMessagePopup)
        {
            ConfigureGenericMessage("DISCONNECTED", message, true, true);
        }
        else if (disconnectionMessageText != null)
        {
            disconnectionMessageText.text = message;
        }

        ShowPopup(PopupKind.Disconnection, popup, rect);
    }

    internal void CloseDisconnectionPopup() => ClosePopup(PopupKind.Disconnection);

    internal void ShowServerError(string message)
    {
        GameObject popup = serverErrorPopup != null ? serverErrorPopup : genericMessagePopup;
        RectTransform rect = serverErrorPopup != null ? serverErrorPopupRect : genericMessagePopupRect;
        if (popup == null) return;

        if (popup == genericMessagePopup)
        {
            ConfigureGenericMessage("SERVER ERROR", message, true, false);
        }
        else if (serverErrorText != null)
        {
            serverErrorText.text = message;
        }

        ShowPopup(PopupKind.ServerError, popup, rect);
    }

    internal void CloseServerErrorPopup() => ClosePopup(PopupKind.ServerError);

    internal void ShowAnotherDeviceError()
    {
        GameObject popup = anotherDevicePopup != null ? anotherDevicePopup : genericMessagePopup;
        RectTransform rect = anotherDevicePopup != null ? anotherDevicePopupRect : genericMessagePopupRect;
        if (popup == null) return;

        if (popup == genericMessagePopup)
        {
            ConfigureGenericMessage(
                "WARNING",
                "Your account has been logged in from another device. This session will be closed.",
                true,
                true);
        }

        ShowPopup(PopupKind.AnotherDevice, popup, rect);
    }

    internal void CloseAnotherDevicePopup() => ClosePopup(PopupKind.AnotherDevice);

    internal void ShowReconnectionPopup(int missedPongs, int maxMissedPongs)
    {
        GameObject popup = reconnectionPopup != null ? reconnectionPopup : genericMessagePopup;
        RectTransform rect = reconnectionPopup != null ? reconnectionPopupRect : genericMessagePopupRect;
        if (popup == null) return;

        string progress = $"Reconnecting ({missedPongs}/{maxMissedPongs})";
        if (popup == genericMessagePopup)
        {
            ConfigureGenericMessage("RECONNECTING", progress, false, false);
        }
        else if (reconnectionText != null)
        {
            reconnectionText.text = progress;
        }

        if (currentPopupKind == PopupKind.Reconnection && currentActivePopup == popup && popup.activeSelf)
        {
            return;
        }

        ShowPopup(PopupKind.Reconnection, popup, rect);
        StartRotation(reconnectionRotatingImage);
    }

    internal void CloseReconnectionPopup() => ClosePopup(PopupKind.Reconnection);

    internal void ShowLoadingPopup()
    {
        ShowLoadingPopup(defaultLoadingDuration);
    }

    internal void ShowLoadingPopup(float duration)
    {
        if (duration < 0f) duration = defaultLoadingDuration;

        GameObject popup = loadingPopup != null ? loadingPopup : genericMessagePopup;
        RectTransform rect = loadingPopup != null ? loadingPopupRect : genericMessagePopupRect;
        if (popup == null) return;

        StopLoadingTimers();
        if (popup == genericMessagePopup)
        {
            ConfigureGenericMessage("LOADING", "Please wait.", false, false);
        }

        ShowPopup(PopupKind.Loading, popup, rect);
        loadingStartTime = Time.realtimeSinceStartup;
        StartRotation(loadingRotatingImage);
        StartLoadingTextAnimation(
            loadingAnimatedText != null ? loadingAnimatedText : popup == genericMessagePopup ? genericMessageText : null,
            "Please wait");

        if (duration > 0f)
        {
            loadingRoutine = StartCoroutine(AutoCloseLoadingPopup(duration));
        }
    }

    internal void CloseLoadingPopup(Action onComplete = null)
    {
        if (onComplete != null) onLoadingClosed += onComplete;
        if (currentPopupKind != PopupKind.Loading || currentActivePopup == null || !currentActivePopup.activeSelf)
        {
            InvokeLoadingClosedCallbacks();
            return;
        }

        float remaining = minLoadingDuration - (Time.realtimeSinceStartup - loadingStartTime);
        if (remaining > 0f)
        {
            if (delayedLoadingCloseRoutine == null)
            {
                delayedLoadingCloseRoutine = StartCoroutine(CloseLoadingAfterDelay(remaining));
            }
            return;
        }

        PerformCloseLoadingPopup();
    }

    internal bool IsLoadingPopupActive()
    {
        return currentPopupKind == PopupKind.Loading && IsActive(currentActivePopup);
    }

    internal void ShowInsufficientBalancePopup()
    {
        GameObject popup = insufficientBalancePopup != null ? insufficientBalancePopup : genericMessagePopup;
        RectTransform rect = insufficientBalancePopup != null ? insufficientBalancePopupRect : genericMessagePopupRect;
        if (popup == null)
        {
            Debug.LogWarning("[PopupManager] Insufficient balance. Assign a popup root to display this message visually.");
            return;
        }

        if (popup == genericMessagePopup)
        {
            ConfigureGenericMessage(
                "INFORMATION",
                "Insufficient balance. Please add funds to continue.",
                true,
                false);
        }

        ShowPopup(PopupKind.InsufficientBalance, popup, rect);
    }

    internal void CloseInsufficientBalancePopup() => ClosePopup(PopupKind.InsufficientBalance);

    internal void ShowExitConfirmationPopup()
    {
        if (exitConfirmationPopup == null)
        {
            Debug.LogError(
                "[PopupManager] Exit confirmation could not open because the Exit Page was not found.");
            return;
        }

        if (exitConfirmed || exitConfirmationClosing ||
            (currentPopupKind == PopupKind.ExitConfirmation && IsActive(exitConfirmationPopup)))
        {
            return;
        }

        if (IsBlockingPopupActive)
        {
            Debug.LogWarning(
                "[PopupManager] Exit confirmation was ignored because another blocking popup is active.");
            return;
        }

        SetExitConfirmationButtonsInteractable(true);
        ShowPopup(PopupKind.ExitConfirmation, exitConfirmationPopup, exitConfirmationPopupRect);
    }

    internal void CloseExitConfirmationPopup()
    {
        if (exitConfirmationClosing || currentPopupKind != PopupKind.ExitConfirmation)
        {
            return;
        }

        exitConfirmationClosing = true;
        SetExitConfirmationButtonsInteractable(false);
        ClosePopup(PopupKind.ExitConfirmation, () =>
        {
            exitConfirmationClosing = false;
            SetExitConfirmationButtonsInteractable(true);
        });
    }

    internal void CloseAllBlockingPopups()
    {
        StopBusyAnimations();
        StopLoadingTimers();
        KillPopupTweens();

        foreach (GameObject popup in GetUniquePopupRoots())
        {
            if (popup != null) popup.SetActive(false);
        }

        ResetCurrentPopup();
        UpdatePopupParentState();
        BlockingStateChanged?.Invoke();
        InvokeLoadingClosedCallbacks();
    }

    private void ConfirmExit()
    {
        if (exitConfirmed || exitConfirmationClosing || currentPopupKind != PopupKind.ExitConfirmation)
        {
            return;
        }

        exitConfirmed = true;
        exitConfirmationClosing = true;
        SetExitConfirmationButtonsInteractable(false);
        audioManager?.PlayNormalClick();
        ClosePopup(PopupKind.ExitConfirmation, () =>
        {
            exitConfirmationClosing = false;
            ExitConfirmed?.Invoke();
        });
    }

    private void CancelExit()
    {
        if (exitConfirmed || exitConfirmationClosing || currentPopupKind != PopupKind.ExitConfirmation)
        {
            return;
        }

        audioManager?.PlayNormalClick();
        CloseExitConfirmationPopup();
    }

    private void SetExitConfirmationButtonsInteractable(bool interactable)
    {
        if (confirmExitButton != null) confirmExitButton.interactable = interactable;
        if (cancelExitButton != null) cancelExitButton.interactable = interactable;
    }

    private void OnGenericOkayClicked()
    {
        bool exitAfterClose = genericOkayExits;
        PopupKind kind = currentPopupKind;
        ClosePopup(kind, () =>
        {
            if (exitAfterClose) ExitConfirmed?.Invoke();
        });
    }

    private void ShowPopup(PopupKind kind, GameObject popup, RectTransform rect)
    {
        if (popup == null) return;

        CloseCurrentPopupImmediately();
        if (popupParent != null) popupParent.SetActive(true);

        currentPopupKind = kind;
        currentActivePopup = popup;
        currentActivePopupRect = rect != null ? rect : popup.GetComponent<RectTransform>();
        popup.SetActive(true);
        AnimatePopupOpen(currentActivePopupRect);
        BlockingStateChanged?.Invoke();
    }

    private void ClosePopup(PopupKind kind, Action onComplete = null)
    {
        if (kind == PopupKind.None || currentPopupKind != kind || currentActivePopup == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (kind == PopupKind.Loading)
        {
            StopBusyAnimations();
            StopLoadingTimers();
        }
        else if (kind == PopupKind.Reconnection)
        {
            StopRotation();
        }

        GameObject popup = currentActivePopup;
        RectTransform rect = currentActivePopupRect;
        AnimatePopupClose(rect, () =>
        {
            if (popup != null) popup.SetActive(false);
            if (currentActivePopup == popup) ResetCurrentPopup();
            UpdatePopupParentState();
            BlockingStateChanged?.Invoke();
            onComplete?.Invoke();
        });
    }

    private void CloseCurrentPopupImmediately()
    {
        if (currentActivePopup == null) return;

        bool closedLoading = currentPopupKind == PopupKind.Loading;
        StopBusyAnimations();
        StopLoadingTimers();
        if (currentActivePopupRect != null)
        {
            currentActivePopupRect.DOKill();
            currentActivePopupRect.localScale = Vector3.one;
        }

        currentActivePopup.SetActive(false);
        ResetCurrentPopup();
        UpdatePopupParentState();
        BlockingStateChanged?.Invoke();
        if (closedLoading) InvokeLoadingClosedCallbacks();
    }

    private void ConfigureGenericMessage(string title, string message, bool showOkayButton, bool exitOnOkay)
    {
        if (genericTitleText != null) genericTitleText.text = title;
        if (genericMessageText != null) genericMessageText.text = message;
        if (genericOkayButton != null) genericOkayButton.gameObject.SetActive(showOkayButton);
        if (genericOkayButtonText != null) genericOkayButtonText.text = exitOnOkay ? "EXIT GAME" : "OKAY";
        genericOkayExits = exitOnOkay;
    }

    private void AnimatePopupOpen(RectTransform popupRect)
    {
        audioManager?.PlayPopupOpen();
        if (popupRect == null) return;

        popupRect.DOKill();
        if (popupScaleInDuration <= 0f)
        {
            popupRect.localScale = Vector3.one;
            return;
        }

        popupRect.localScale = Vector3.zero;
        popupRect.DOScale(1f, popupScaleInDuration).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void AnimatePopupClose(RectTransform popupRect, Action onComplete)
    {
        if (popupRect == null || popupScaleOutDuration <= 0f)
        {
            if (popupRect != null) popupRect.localScale = Vector3.one;
            onComplete?.Invoke();
            return;
        }

        popupRect.DOKill();
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(popupRect.DOScale(1.05f, 0.08f));
        sequence.Append(popupRect.DOScale(0f, popupScaleOutDuration).SetEase(Ease.InBack));
        sequence.OnComplete(() =>
        {
            popupRect.localScale = Vector3.one;
            onComplete?.Invoke();
        });
    }

    private void StartRotation(Image targetImage)
    {
        StopRotation();
        if (targetImage != null) rotationRoutine = StartCoroutine(RotateImageCoroutine(targetImage));
    }

    private IEnumerator RotateImageCoroutine(Image targetImage)
    {
        while (targetImage != null)
        {
            targetImage.transform.Rotate(0f, 0f, -rotationSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        rotationRoutine = null;
    }

    private void StopRotation()
    {
        if (rotationRoutine == null) return;
        StopCoroutine(rotationRoutine);
        rotationRoutine = null;
    }

    private void StartLoadingTextAnimation(TMP_Text target, string prefix)
    {
        StopLoadingTextAnimation();
        if (target == null) return;

        activeLoadingText = target;
        activeLoadingTextPrefix = prefix;
        loadingTextRoutine = StartCoroutine(LoadingTextCoroutine());
    }

    private IEnumerator LoadingTextCoroutine()
    {
        int dotCount = 1;
        while (activeLoadingText != null)
        {
            activeLoadingText.text = activeLoadingTextPrefix + new string('.', dotCount);
            dotCount = dotCount % 3 + 1;
            yield return new WaitForSecondsRealtime(loadingTextCycleSpeed);
        }

        loadingTextRoutine = null;
    }

    private void StopLoadingTextAnimation()
    {
        if (loadingTextRoutine != null)
        {
            StopCoroutine(loadingTextRoutine);
            loadingTextRoutine = null;
        }

        activeLoadingText = null;
    }

    private void StopBusyAnimations()
    {
        StopRotation();
        StopLoadingTextAnimation();
    }

    private IEnumerator AutoCloseLoadingPopup(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        loadingRoutine = null;
        CloseLoadingPopup();
    }

    private IEnumerator CloseLoadingAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        delayedLoadingCloseRoutine = null;
        PerformCloseLoadingPopup();
    }

    private void PerformCloseLoadingPopup()
    {
        StopBusyAnimations();
        StopLoadingTimers();
        ClosePopup(PopupKind.Loading, InvokeLoadingClosedCallbacks);
    }

    private void StopLoadingTimers()
    {
        if (loadingRoutine != null)
        {
            StopCoroutine(loadingRoutine);
            loadingRoutine = null;
        }

        if (delayedLoadingCloseRoutine != null)
        {
            StopCoroutine(delayedLoadingCloseRoutine);
            delayedLoadingCloseRoutine = null;
        }
    }

    private void InvokeLoadingClosedCallbacks()
    {
        Action callbacks = onLoadingClosed;
        onLoadingClosed = null;
        callbacks?.Invoke();
    }

    private void ResolveReferences()
    {
        genericMessagePopup = ResolveOptionalRoot(genericMessagePopup, "ErrorPopup", "Error Popup");
        loadingPopup = ResolveOptionalRoot(loadingPopup, "LoadingPopup", "Loading Popup");
        disconnectionPopup = ResolveOptionalRoot(disconnectionPopup, "DisconnectionPopup", "Disconnection Popup");
        serverErrorPopup = ResolveOptionalRoot(serverErrorPopup, "ServerErrorPopup", "Server Error Popup");
        anotherDevicePopup = ResolveOptionalRoot(anotherDevicePopup, "AnotherDevicePopup", "Another Device Popup");
        reconnectionPopup = ResolveOptionalRoot(reconnectionPopup, "ReconnectionPopup", "Reconnection Popup");
        insufficientBalancePopup = ResolveOptionalRoot(
            insufficientBalancePopup,
            "InsufficientBalancePopup",
            "Insufficient Balance Popup");
        exitConfirmationPopup = ResolveOptionalRoot(
            exitConfirmationPopup,
            "Exit Page",
            "ExitConfirmationPopup",
            "Exit Confirmation Popup");

        genericMessagePopupRect = ResolveRect(genericMessagePopupRect, genericMessagePopup);
        loadingPopupRect = ResolveRect(loadingPopupRect, loadingPopup);
        disconnectionPopupRect = ResolveRect(disconnectionPopupRect, disconnectionPopup);
        serverErrorPopupRect = ResolveRect(serverErrorPopupRect, serverErrorPopup);
        anotherDevicePopupRect = ResolveRect(anotherDevicePopupRect, anotherDevicePopup);
        reconnectionPopupRect = ResolveRect(reconnectionPopupRect, reconnectionPopup);
        insufficientBalancePopupRect = ResolveRect(insufficientBalancePopupRect, insufficientBalancePopup);
        exitConfirmationPopupRect = ResolveRect(exitConfirmationPopupRect, exitConfirmationPopup);

        confirmExitButton = ResolveChild(
            confirmExitButton,
            exitConfirmationPopup,
            "Yes",
            "YES");
        cancelExitButton = ResolveChild(
            cancelExitButton,
            exitConfirmationPopup,
            "No",
            "NO");

        genericTitleText = ResolveChild(genericTitleText, genericMessagePopup, "Title");
        genericMessageText = ResolveChild(genericMessageText, genericMessagePopup, "Description", "Message");
        genericOkayButton = ResolveChild(genericOkayButton, genericMessagePopup, "OkayButton", "OKButton", "Okay Button");
        if (genericOkayButtonText == null && genericOkayButton != null)
        {
            genericOkayButtonText = genericOkayButton.GetComponentInChildren<TMP_Text>(true);
        }

        audioManager = audioManager != null ? audioManager : FindSceneComponent<AudioManager>();

        if (exitConfirmationPopup == null)
        {
            Debug.LogWarning("[PopupManager] Exit Page was not found in the active scene.");
        }
        if (confirmExitButton == null || cancelExitButton == null)
        {
            Debug.LogWarning("[PopupManager] Exit confirmation Yes/No buttons were not found.");
        }
    }

    private void HideAllPopupsImmediately()
    {
        StopBusyAnimations();
        StopLoadingTimers();
        foreach (GameObject popup in GetUniquePopupRoots())
        {
            if (popup == null) continue;
            popup.SetActive(false);
            RectTransform rect = popup.GetComponent<RectTransform>();
            if (rect != null) rect.localScale = Vector3.one;
        }

        ResetCurrentPopup();
        if (popupParent != null) popupParent.SetActive(false);
    }

    private HashSet<GameObject> GetUniquePopupRoots()
    {
        return new HashSet<GameObject>
        {
            genericMessagePopup,
            loadingPopup,
            disconnectionPopup,
            serverErrorPopup,
            anotherDevicePopup,
            reconnectionPopup,
            insufficientBalancePopup,
            exitConfirmationPopup
        };
    }

    private void UpdatePopupParentState()
    {
        if (popupParent == null) return;

        bool anyActive = false;
        foreach (GameObject popup in GetUniquePopupRoots())
        {
            if (popup != null && popup.activeSelf)
            {
                anyActive = true;
                break;
            }
        }

        popupParent.SetActive(anyActive);
    }

    private void ResetCurrentPopup()
    {
        currentActivePopup = null;
        currentActivePopupRect = null;
        currentPopupKind = PopupKind.None;
    }

    private void KillPopupTweens()
    {
        foreach (RectTransform rect in new[]
                 {
                     genericMessagePopupRect,
                     loadingPopupRect,
                     disconnectionPopupRect,
                     serverErrorPopupRect,
                     anotherDevicePopupRect,
                     reconnectionPopupRect,
                     insufficientBalancePopupRect,
                     exitConfirmationPopupRect
                 })
        {
            if (rect != null) rect.DOKill();
        }
    }

    private static RectTransform ResolveRect(RectTransform assigned, GameObject root)
    {
        return assigned != null ? assigned : root != null ? root.GetComponent<RectTransform>() : null;
    }

    private static T ResolveChild<T>(T assigned, GameObject root, params string[] names) where T : Component
    {
        if (assigned != null || root == null) return assigned;

        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (string objectName in names)
            {
                if (candidate.name != objectName) continue;
                T component = candidate.GetComponent<T>();
                if (component != null) return component;
            }
        }

        return null;
    }

    private static bool IsActive(GameObject popup)
    {
        return popup != null && popup.activeInHierarchy;
    }

    private static GameObject ResolveOptionalRoot(GameObject assigned, params string[] names)
    {
        if (assigned != null) return assigned;

        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid()) continue;
            foreach (string objectName in names)
            {
                if (candidate.name == objectName) return candidate.gameObject;
            }
        }

        return null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
        {
            if (candidate != null && candidate.gameObject.scene.IsValid()) return candidate;
        }

        return null;
    }
}
