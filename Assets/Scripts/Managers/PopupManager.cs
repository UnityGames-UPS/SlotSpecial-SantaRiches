using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PopupManager : MonoBehaviour
{
    [Header("Optional Existing Popup Roots")]
    [SerializeField] private GameObject loadingPopup;
    [SerializeField] private GameObject disconnectionPopup;
    [SerializeField] private GameObject serverErrorPopup;
    [SerializeField] private GameObject anotherDevicePopup;
    [SerializeField] private GameObject reconnectionPopup;
    [SerializeField] private GameObject insufficientBalancePopup;
    [SerializeField] private GameObject exitConfirmationPopup;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text serverErrorText;
    [SerializeField] private TMP_Text reconnectionText;

    [Header("Optional Exit Confirmation Buttons")]
    [SerializeField] private Button confirmExitButton;
    [SerializeField] private Button cancelExitButton;

    internal event Action BlockingStateChanged;
    internal event Action ExitConfirmed;

    private Coroutine loadingRoutine;

    internal bool IsBlockingPopupActive => IsActive(loadingPopup) || IsActive(disconnectionPopup) || IsActive(serverErrorPopup) ||
        IsActive(anotherDevicePopup) || IsActive(reconnectionPopup) ||
        IsActive(insufficientBalancePopup) || IsActive(exitConfirmationPopup);

    private void Awake()
    {
        loadingPopup = ResolveOptionalRoot(loadingPopup, "LoadingPopup", "Loading Popup");
        disconnectionPopup = ResolveOptionalRoot(disconnectionPopup, "DisconnectionPopup", "Disconnection Popup");
        serverErrorPopup = ResolveOptionalRoot(serverErrorPopup, "ServerErrorPopup", "Server Error Popup");
        anotherDevicePopup = ResolveOptionalRoot(anotherDevicePopup, "AnotherDevicePopup", "Another Device Popup");
        reconnectionPopup = ResolveOptionalRoot(reconnectionPopup, "ReconnectionPopup", "Reconnection Popup");
        insufficientBalancePopup = ResolveOptionalRoot(insufficientBalancePopup, "InsufficientBalancePopup", "Insufficient Balance Popup");
        exitConfirmationPopup = ResolveOptionalRoot(exitConfirmationPopup, "ExitConfirmationPopup", "Exit Confirmation Popup");
    }

    private void OnEnable()
    {
        if (confirmExitButton != null) confirmExitButton.onClick.AddListener(ConfirmExit);
        if (cancelExitButton != null) cancelExitButton.onClick.AddListener(CloseExitConfirmationPopup);
    }

    private void OnDisable()
    {
        if (confirmExitButton != null) confirmExitButton.onClick.RemoveListener(ConfirmExit);
        if (cancelExitButton != null) cancelExitButton.onClick.RemoveListener(CloseExitConfirmationPopup);

        if (loadingRoutine != null)
        {
            StopCoroutine(loadingRoutine);
            loadingRoutine = null;
        }
    }

    internal void CloseReconnectionPopup()
    {
        SetPopupActive(reconnectionPopup, false);
    }

    internal void ShowLoadingPopup(float duration)
    {
        if (loadingRoutine != null) StopCoroutine(loadingRoutine);
        SetPopupActive(loadingPopup, true);
        if (duration > 0f)
        {
            loadingRoutine = StartCoroutine(HideLoadingAfter(duration));
        }
    }

    internal void ShowDisconnectionPopup()
    {
        SetPopupActive(disconnectionPopup, true);
    }

    internal void CloseDisconnectionPopup()
    {
        SetPopupActive(disconnectionPopup, false);
    }

    internal void ShowServerError(string message)
    {
        if (serverErrorText != null) serverErrorText.text = message;
        SetPopupActive(serverErrorPopup, true);
    }

    internal void CloseServerErrorPopup()
    {
        SetPopupActive(serverErrorPopup, false);
    }

    internal void ShowAnotherDeviceError()
    {
        SetPopupActive(anotherDevicePopup, true);
    }

    internal void CloseAnotherDevicePopup()
    {
        SetPopupActive(anotherDevicePopup, false);
    }

    internal void ShowReconnectionPopup(int missedPongs, int maxMissedPongs)
    {
        if (reconnectionText != null)
        {
            reconnectionText.text = $"Reconnecting ({missedPongs}/{maxMissedPongs})";
        }
        SetPopupActive(reconnectionPopup, true);
    }

    internal bool IsLoadingPopupActive()
    {
        return IsActive(loadingPopup);
    }

    internal void ShowInsufficientBalancePopup()
    {
        SetPopupActive(insufficientBalancePopup, true);
        if (insufficientBalancePopup == null)
        {
            Debug.LogWarning("[PopupManager] Insufficient balance. Assign a popup root to display this message visually.");
        }
    }

    internal void CloseInsufficientBalancePopup()
    {
        SetPopupActive(insufficientBalancePopup, false);
    }

    internal void ShowExitConfirmationPopup()
    {
        SetPopupActive(exitConfirmationPopup, true);
        if (exitConfirmationPopup == null)
        {
            Debug.LogWarning("[PopupManager] Assign an Exit Confirmation Popup before enabling the Home action.");
        }
    }

    internal void CloseExitConfirmationPopup()
    {
        SetPopupActive(exitConfirmationPopup, false);
    }

    internal void CloseAllBlockingPopups()
    {
        SetActiveWithoutNotify(loadingPopup, false);
        SetActiveWithoutNotify(disconnectionPopup, false);
        SetActiveWithoutNotify(serverErrorPopup, false);
        SetActiveWithoutNotify(anotherDevicePopup, false);
        SetActiveWithoutNotify(reconnectionPopup, false);
        SetActiveWithoutNotify(insufficientBalancePopup, false);
        SetActiveWithoutNotify(exitConfirmationPopup, false);
        BlockingStateChanged?.Invoke();
    }

    private void ConfirmExit()
    {
        CloseExitConfirmationPopup();
        ExitConfirmed?.Invoke();
    }

    private System.Collections.IEnumerator HideLoadingAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        loadingRoutine = null;
        SetPopupActive(loadingPopup, false);
    }

    private void SetPopupActive(GameObject popup, bool active)
    {
        if (popup != null)
        {
            popup.SetActive(active);
        }

        BlockingStateChanged?.Invoke();
    }

    private static void SetActiveWithoutNotify(GameObject popup, bool active)
    {
        if (popup != null) popup.SetActive(active);
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
}
