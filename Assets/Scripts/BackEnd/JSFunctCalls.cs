using System.Runtime.InteropServices;
using UnityEngine;

public class JSFunctCalls : MonoBehaviour
{
  [DllImport("__Internal")] private static extern void SendLogToReactNative(string message);

  [DllImport("__Internal")] private static extern void SendPostMessage(string message);

  [DllImport("__Internal")] private static extern void RegisterVisibilityChangeListener(string gameObjectName);

#if UNITY_WEBGL && !UNITY_EDITOR
  [DllImport("__Internal")] private static extern void RequestFullscreen();

  [DllImport("__Internal")] private static extern void ExitFullscreen();

  [DllImport("__Internal")] private static extern void RegisterFullscreenChangeListener(string gameObjectName);

  [DllImport("__Internal")] private static extern void UnregisterFullscreenChangeListener();
#endif

  void OnEnable()
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    Application.logMessageReceived += HandleLog;
#endif
  }

  void OnDisable()
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    Application.logMessageReceived -= HandleLog;
#endif
  }

#if UNITY_WEBGL && !UNITY_EDITOR
  void HandleLog(string logString, string stackTrace, LogType type)
  {
    string formattedMessage = $"[{type}] {logString}";
    SendLogToReactNative(formattedMessage);
  }
#endif

  internal void SendCustomMessage(string message)
  {
    if (string.IsNullOrWhiteSpace(message))
    {
      Debug.LogWarning("[JS] Ignored an empty platform message.");
      return;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    SendPostMessage(message);
#else
    Debug.Log($"[JS] Platform message: {message}");
#endif
  }

  internal void RegisterVisibilityListener(string gameObjectName)
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    Debug.Log($"[JS] Registering visibility change listener on '{gameObjectName}'");
    RegisterVisibilityChangeListener(gameObjectName);
#else
    Debug.Log("[JS] Visibility listener not registered (editor mode)");
#endif
  }

  /// <summary>Requests browser fullscreen (expand).</summary>
  internal void RequestExpandGame()
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    Debug.Log("[JS] Requesting fullscreen expand");
    RequestFullscreen();
#else
    Debug.Log("[JS] Would request fullscreen (editor mode)");
#endif
  }

  /// <summary>Exits browser fullscreen (shrink).</summary>
  internal void RequestShrinkGame()
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    Debug.Log("[JS] Requesting exit fullscreen (shrink)");
    ExitFullscreen();
#else
    Debug.Log("[JS] Would exit fullscreen (editor mode)");
#endif
  }

  internal void RegisterFullscreenListener(string gameObjectName)
  {
    if (string.IsNullOrWhiteSpace(gameObjectName))
    {
      Debug.LogWarning("[Fullscreen] Cannot register the browser listener without a GameObject name.");
      return;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    RegisterFullscreenChangeListener(gameObjectName);
#else
    Debug.Log($"[Fullscreen] Editor/non-WebGL: would register the browser listener for '{gameObjectName}'.");
#endif
  }

  internal void UnregisterFullscreenListener()
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    UnregisterFullscreenChangeListener();
#else
    Debug.Log("[Fullscreen] Editor/non-WebGL: would unregister the browser listener.");
#endif
  }
}
