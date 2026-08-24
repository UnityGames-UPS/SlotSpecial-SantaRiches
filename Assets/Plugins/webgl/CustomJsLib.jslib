mergeInto(LibraryManager.library, {
    SendLogToReactNative: function (messagePtr) {
        var message = UTF8ToString(messagePtr);
        if (window.ReactNativeWebView) {
          window.ReactNativeWebView.postMessage(message);
        } 
    },

    SendPostMessage: function(messagePtr) {
      var message = UTF8ToString(messagePtr);
      if(window.ReactNativeWebView){
        if(message == "authToken"){
          window.ReactNativeWebView.postMessage("if message is authtoken");
          var injectedObjectJson = window.ReactNativeWebView.injectedObjectJson();
          var injectedObj = JSON.parse(injectedObjectJson);

          window.ReactNativeWebView.postMessage('Injected obj : ' + injectedObjectJson);
          
          var combinedData = JSON.stringify({
              socketURL: injectedObj.socketURL.trim(),
              cookie: injectedObj.token.trim(),
              nameSpace: injectedObj.nameSpace ? injectedObj.nameSpace.trim() : ""
          });

          if (typeof SendMessage === 'function') {
            SendMessage('SocketManager', 'ReceiveAuthToken', combinedData);
          }
        }
        window.ReactNativeWebView.postMessage(message);
      }
      else if(window.parent){
        if(window.parent.dispatchReactUnityEvent){
          console.log("Inside window parent");
          window.parent.dispatchReactUnityEvent(message); 
        }
      }
    },

    RequestFullscreen: function () {
      var element = document.documentElement;
      var request = element.requestFullscreen
                 || element.webkitRequestFullscreen
                 || element.mozRequestFullScreen
                 || element.msRequestFullscreen;
      var bridge = window.__unityFullscreenBridge;

      function reportFailure(error) {
        console.warn('[Fullscreen] Browser fullscreen request failed.', error || 'Fullscreen API unavailable.');
        if (bridge && typeof bridge.sendState === 'function') {
          bridge.sendState(false);
        }
      }

      if (!request) {
        reportFailure('No supported requestFullscreen API was found.');
        return;
      }

      try {
        var result = request.call(element);
        if (result && typeof result.catch === 'function') {
          result.catch(reportFailure);
        }
      } catch (error) {
        reportFailure(error);
      }
    },

    ExitFullscreen: function () {
      var exit = document.exitFullscreen
              || document.webkitExitFullscreen
              || document.mozCancelFullScreen
              || document.msExitFullscreen;
      var bridge = window.__unityFullscreenBridge;

      function reportFailure(error) {
        console.warn('[Fullscreen] Browser fullscreen exit failed.', error || 'Fullscreen API unavailable.');
        if (bridge && typeof bridge.sendCurrentState === 'function') {
          bridge.sendCurrentState();
        }
      }

      if (!exit) {
        reportFailure('No supported exitFullscreen API was found.');
        return;
      }

      try {
        var result = exit.call(document);
        if (result && typeof result.catch === 'function') {
          result.catch(reportFailure);
        }
      } catch (error) {
        reportFailure(error);
      }
    },

    RegisterFullscreenChangeListener: function(gameObjectNamePtr) {
      var gameObjectName = UTF8ToString(gameObjectNamePtr);
      var eventNames = [
        'fullscreenchange',
        'webkitfullscreenchange',
        'mozfullscreenchange',
        'MSFullscreenChange'
      ];
      var bridge = window.__unityFullscreenBridge || {};

      if (bridge.listener) {
        for (var oldIndex = 0; oldIndex < eventNames.length; oldIndex++) {
          document.removeEventListener(eventNames[oldIndex], bridge.listener);
        }
      }

      bridge.gameObjectName = gameObjectName;
      bridge.isFullscreen = function () {
        return !!(document.fullscreenElement
          || document.webkitFullscreenElement
          || document.mozFullScreenElement
          || document.msFullscreenElement);
      };
      bridge.sendState = function (isFullscreen) {
        try {
          var unityInstance = window.unityInstance;
          if ((!unityInstance || typeof unityInstance.SendMessage !== 'function')
              && typeof Module !== 'undefined'
              && Module
              && typeof Module.SendMessage === 'function') {
            unityInstance = Module;
          }

          if (unityInstance && typeof unityInstance.SendMessage === 'function') {
            unityInstance.SendMessage(
              bridge.gameObjectName,
              'OnFullscreenChanged',
              isFullscreen ? '1' : '0');
          } else if (typeof SendMessage === 'function') {
            SendMessage(
              bridge.gameObjectName,
              'OnFullscreenChanged',
              isFullscreen ? '1' : '0');
          } else {
            console.warn('[Fullscreen] Unity instance is not ready; state could not be delivered.');
          }
        } catch (error) {
          console.warn('[Fullscreen] Failed to send state to Unity.', error);
        }
      };
      bridge.sendCurrentState = function () {
        bridge.sendState(bridge.isFullscreen());
      };
      bridge.listener = function () {
        bridge.sendCurrentState();
      };

      window.__unityFullscreenBridge = bridge;

      for (var index = 0; index < eventNames.length; index++) {
        document.addEventListener(eventNames[index], bridge.listener);
      }

      bridge.sendCurrentState();
    },

    UnregisterFullscreenChangeListener: function () {
      var bridge = window.__unityFullscreenBridge;
      if (!bridge || !bridge.listener) {
        return;
      }

      var eventNames = [
        'fullscreenchange',
        'webkitfullscreenchange',
        'mozfullscreenchange',
        'MSFullscreenChange'
      ];

      for (var index = 0; index < eventNames.length; index++) {
        document.removeEventListener(eventNames[index], bridge.listener);
      }

      bridge.listener = null;
      bridge.gameObjectName = null;
      bridge.sendState = null;
      bridge.sendCurrentState = null;
    },

    RegisterVisibilityChangeListener: function(gameObjectNamePtr) {
        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        console.log('[JS] RegisterVisibilityChangeListener called for GameObject:', gameObjectName);

        function setUnityAudioSuspended(suspended) {
            try {
                var wa = (typeof WEBAudio !== 'undefined') ? WEBAudio
                       : (typeof Module !== 'undefined' && Module.WEBAudio) ? Module.WEBAudio
                       : null;
                if (!wa || !wa.audioContext) return;
                if (suspended) {
                    if (wa.audioContext.state === 'running') wa.audioContext.suspend();
                } else {
                    if (wa.audioContext.state === 'suspended') wa.audioContext.resume();
                }
            } catch (err) { console.warn('[JS] Unity audio suspend/resume failed:', err); }
        }

        function sendFocusToUnity(focused) {
            setUnityAudioSuspended(!focused);
            try {
                var value = focused ? '1' : '0';
                if (typeof SendMessage === 'function') {
                    SendMessage(gameObjectName, 'OnFocusChanged', value);
                } else if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
                    unityInstance.SendMessage(gameObjectName, 'OnFocusChanged', value);
                }
            } catch (err) {
                console.error('[JS] Error sending focus message to Unity:', err);
            }
        }

        window._unityVisibilityCallback = function() {
            var hidden = document.hidden || document.webkitHidden;
            sendFocusToUnity(!hidden);
        };
        window._unityWindowBlurCallback  = function() { sendFocusToUnity(false); };
        window._unityWindowFocusCallback = function() { sendFocusToUnity(true); };

        document.removeEventListener('visibilitychange',       window._unityVisibilityCallback);
        document.removeEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
        window.removeEventListener('blur',  window._unityWindowBlurCallback);
        window.removeEventListener('focus', window._unityWindowFocusCallback);

        document.addEventListener('visibilitychange',       window._unityVisibilityCallback);
        document.addEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
        window.addEventListener('blur',  window._unityWindowBlurCallback);
        window.addEventListener('focus', window._unityWindowFocusCallback);
    }
});
