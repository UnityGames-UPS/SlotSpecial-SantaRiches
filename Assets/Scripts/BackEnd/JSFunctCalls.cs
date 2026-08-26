using System.Runtime.InteropServices;
using UnityEngine;

public class JSFunctCalls : MonoBehaviour
{
  [DllImport("__Internal")] private static extern void SendPostMessage(string message);

  [DllImport("__Internal")] private static extern void RegisterVisibilityChangeListener(string gameObjectName);

  [DllImport("__Internal")] private static extern void RegisterResizeListener(string gameObjectName, string methodName);

  [DllImport("__Internal")] private static extern void RegisterTokenListener(string gameObjectName, string methodName);

#if UNITY_WEBGL && !UNITY_EDITOR
  [DllImport("__Internal")] private static extern void RequestFullscreen();

  [DllImport("__Internal")] private static extern void ExitFullscreen();

  [DllImport("__Internal")] private static extern void RegisterFullscreenChangeListener(string gameObjectName);

  [DllImport("__Internal")] private static extern void UnregisterFullscreenChangeListener();
#endif

  private void Start()
  {
    RegisterDimensionsListener();
  }

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

  internal void RegisterDimensionsListener(string gameObjectName = "OC", string methodName = "SwitchDisplay")
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    RegisterResizeListener(gameObjectName, methodName);
#else
    Debug.Log($"[JS] Resize listener not registered ('{gameObjectName}.{methodName}', editor mode)");
#endif
  }

  internal void RegisterAuthTokenListener(string gameObjectName, string methodName = "ReceiveAuthToken")
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    RegisterTokenListener(gameObjectName, methodName);
#else
    Debug.Log($"[JS] Token listener not registered ('{gameObjectName}.{methodName}', editor mode)");
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
