using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Gree.UnityWebView;
using RobotAstro;
using UnityEngine;

namespace FranklinGame.Menu.Web
{
    /// <summary>
    /// Owns the single local native WebView used by the front end. Android and
    /// iOS display a native overlay; macOS Editor uses the plugin's Game-view
    /// texture path so the exact same local bundle can be clicked in the Editor.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FranklinWebMenuBridge))]
    public sealed class FranklinWebMenuHost : MonoBehaviour
    {
        private const string RelativeIndexPath = "FranklinWebMenu/index.html";
        private const string FallbackFileName = "fallback.html";
        private const float InitializationTimeoutSeconds = 12f;
        private const float PageLoadTimeoutSeconds = 12f;
        private const float JavaScriptReadyTimeoutSeconds = 4f;
        private const float FallbackLoadTimeoutSeconds = 6f;
        private const string DenyEveryOtherNavigationPattern = ".*";

        [SerializeField] private FranklinWebMenuBridge m_Bridge;
        [SerializeField] private bool m_LogWebErrors = true;

        private WebViewObject m_WebView;
        private bool m_PageLoaded;
        private bool m_JavascriptReady;
        private bool m_IsFallbackPage;
        private bool m_FallbackRequested;
        private bool m_ShuttingDown;
        private string m_IndexUrl;
        private string m_LocalBaseUrl;
        private string m_FallbackUrl;
        private string m_PendingStateJson;
        private Rect m_LastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private int m_LastScreenWidth = -1;
        private int m_LastScreenHeight = -1;
        private bool m_ApplicationPaused;
        private bool m_ApplicationFocused = true;

        public bool IsReady => this.m_WebView != null && this.m_PageLoaded &&
            this.m_JavascriptReady && !this.m_IsFallbackPage;

        private void Awake()
        {
            if (this.m_Bridge == null)
            {
                this.m_Bridge = this.GetComponent<FranklinWebMenuBridge>();
            }

            DynamicResolutionScaler.SetMenuSuspended(true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            this.m_PendingStateJson = this.m_Bridge.BuildStateJson();
            this.m_Bridge.StateJsonChanged += this.OnStateJsonChanged;
            this.m_Bridge.ActionReceived += this.OnBridgeActionReceived;
        }

        private IEnumerator Start()
        {
#if !(UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_ANDROID)
            Debug.LogError(
                "Franklin web menu requires macOS Editor, Android, or iOS.",
                this
            );
            yield break;
#else
            try
            {
                if (!WebViewObject.IsWebViewAvailable())
                {
                    Debug.LogError("No native WebView is available on this device.", this);
                    yield break;
                }

                this.m_IndexUrl = BuildIndexUrl();
                int finalSlash = this.m_IndexUrl.LastIndexOf('/');
                this.m_LocalBaseUrl = finalSlash >= 0
                    ? this.m_IndexUrl.Substring(0, finalSlash + 1)
                    : this.m_IndexUrl;
                this.m_FallbackUrl = this.m_LocalBaseUrl + FallbackFileName;
                GameObject nativeView = new GameObject("FranklinWebMenuNativeView");
                nativeView.transform.SetParent(this.transform, false);
                this.m_WebView = nativeView.AddComponent<WebViewObject>();

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                // macOS Editor captures WKWebView into the Game view. One refresh per
                // rendered frame preserves click feedback; mobile uses a native overlay
                // and never pays this texture-copy cost.
                this.m_WebView.bitmapRefreshCycle = 1;
                this.m_WebView.devicePixelRatio = 1;
#endif

                this.m_WebView.Init(
                    cb: this.OnJavaScriptMessage,
                    err: this.OnWebError,
                    httpErr: this.OnWebHttpError,
                    ld: this.OnPageLoaded,
                    started: this.OnPageStarted,
                    hooked: this.OnNavigationHooked,
                    transparent: false,
                    zoom: false,
                    androidForceDarkMode: 1,
                    enableWKWebView: true,
                    wkContentMode: 1,
                    wkAllowsLinkPreview: false,
                    wkAllowsBackForwardNavigationGestures: false,
                    separated: false
                );
            }
            catch (Exception exception)
            {
                Debug.LogError("Native WebView setup failed: " + exception, this);
                this.DisposeWebView();
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;
            while (this.m_WebView != null && !this.m_WebView.IsInitialized() &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (this.m_WebView == null || !this.m_WebView.IsInitialized())
            {
                Debug.LogError("Native WebView initialization timed out.", this);
                this.DisposeWebView();
                yield break;
            }

            // Only the packaged file bundle and the internal unity: bridge may
            // navigate. Remote pages/CDNs are intentionally denied.
            try
            {
                string localNavigationAllowPattern =
                    "^(?:unity:.*|" + Regex.Escape(this.m_LocalBaseUrl) + ".*)$";
                if (!this.m_WebView.SetURLPattern(
                        localNavigationAllowPattern,
                        DenyEveryOtherNavigationPattern,
                        string.Empty))
                {
                    Debug.LogError(
                        "Native WebView navigation policy could not be applied.",
                        this
                    );
                    this.DisposeWebView();
                    yield break;
                }
                this.m_WebView.SetAlertDialogEnabled(false);
                this.m_WebView.SetScrollbarsVisibility(false);
                this.m_WebView.SetMargins(0, 0, 0, 0);
                // Avoid exposing the native view's blank/default page. The allowed
                // local document becomes visible only from OnPageLoaded.
                this.m_WebView.SetVisibility(false);
                this.m_WebView.LoadURL(this.m_IndexUrl);
            }
            catch (Exception exception)
            {
                Debug.LogError("Native WebView could not load the local menu: " + exception, this);
                this.DisposeWebView();
                yield break;
            }

            float pageDeadline = Time.realtimeSinceStartup + PageLoadTimeoutSeconds;
            while (this.m_WebView != null && !this.m_PageLoaded &&
                   Time.realtimeSinceStartup < pageDeadline)
            {
                yield return null;
            }
            if (this.m_WebView != null && !this.m_PageLoaded)
            {
                Debug.LogError("Local Franklin web menu load timed out.", this);
                this.LoadFallbackPage();
            }

            if (this.m_WebView == null) yield break;
            if (!this.m_PageLoaded)
            {
                float fallbackDeadline =
                    Time.realtimeSinceStartup + FallbackLoadTimeoutSeconds;
                while (this.m_WebView != null && !this.m_PageLoaded &&
                       Time.realtimeSinceStartup < fallbackDeadline)
                {
                    yield return null;
                }
                if (this.m_WebView != null && !this.m_PageLoaded)
                {
                    Debug.LogError("Local Franklin fallback page also failed to load.", this);
                }
                yield break;
            }
            if (this.m_IsFallbackPage) yield break;

            float readyDeadline =
                Time.realtimeSinceStartup + JavaScriptReadyTimeoutSeconds;
            while (this.m_WebView != null && !this.m_JavascriptReady &&
                   Time.realtimeSinceStartup < readyDeadline)
            {
                yield return null;
            }
            if (this.m_WebView != null && !this.m_JavascriptReady)
            {
                Debug.LogError(
                    "Franklin web menu loaded without a working JavaScript bridge.",
                    this
                );
                this.LoadFallbackPage();
                float fallbackDeadline =
                    Time.realtimeSinceStartup + FallbackLoadTimeoutSeconds;
                while (this.m_WebView != null && !this.m_PageLoaded &&
                       Time.realtimeSinceStartup < fallbackDeadline)
                {
                    yield return null;
                }
                if (this.m_WebView != null && !this.m_PageLoaded)
                {
                    Debug.LogError("Local Franklin fallback page also failed to load.", this);
                }
            }
#endif
        }

        private void OnEnable()
        {
            this.TryRevealPage();
            this.ApplyAudioSuspensionToPage();
        }

        private void OnDisable()
        {
            if (!this.m_ShuttingDown && this.m_WebView != null)
            {
                this.ApplyAudioSuspensionToPage(true);
                this.m_WebView.SetVisibility(false);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            this.m_ApplicationPaused = pauseStatus;
            this.ApplyAudioSuspensionToPage();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            this.m_ApplicationFocused = hasFocus;
            this.ApplyAudioSuspensionToPage();
        }

        private void LateUpdate()
        {
            if (!this.IsReady) return;
            if (this.m_LastScreenWidth == Screen.width &&
                this.m_LastScreenHeight == Screen.height &&
                this.m_LastSafeArea == Screen.safeArea)
            {
                return;
            }

            this.ApplyHostMetricsToPage();
        }

        private void OnDestroy()
        {
            this.m_ShuttingDown = true;
            DynamicResolutionScaler.SetMenuSuspended(false);
            if (this.m_Bridge != null)
            {
                this.m_Bridge.StateJsonChanged -= this.OnStateJsonChanged;
                this.m_Bridge.ActionReceived -= this.OnBridgeActionReceived;
            }
            this.DisposeWebView();
        }

        public void ShowToast(string message, string tone = "info")
        {
            if (!this.IsReady || string.IsNullOrWhiteSpace(message)) return;
            string json = JsonUtility.ToJson(new TextEnvelope
            {
                value = message,
                tone = string.IsNullOrWhiteSpace(tone) ? "info" : tone
            });
            this.m_WebView.EvaluateJS(
                "(function(x){window.FranklinMenu&&window.FranklinMenu.notify" +
                "(x.value,x.tone);})(" + json + ");"
            );
        }

        public void ShowLocalizedToast(
            string key,
            string fallback,
            string tone = "info"
        )
        {
            if (!this.IsReady || string.IsNullOrWhiteSpace(key)) return;
            string json = JsonUtility.ToJson(new TextEnvelope
            {
                key = key,
                value = fallback ?? key,
                tone = string.IsNullOrWhiteSpace(tone) ? "info" : tone
            });
            this.m_WebView.EvaluateJS(
                "(function(x){var m=window.FranklinMenu,i=window.FranklinMenuI18n;" +
                "if(m){m.notify(i&&i.t?i.t(x.key,x.value):x.value,x.tone);}})(" +
                json + ");"
            );
        }

        public void PlaySound(string cue)
        {
            if (!this.IsReady || !IsAllowedAudioCue(cue)) return;
            string json = JsonUtility.ToJson(new TextEnvelope { value = cue });
            this.m_WebView.EvaluateJS(
                "(function(x){window.FranklinMenu&&window.FranklinMenu.playSound" +
                "(x.value);})(" + json + ");"
            );
        }

        public void SetBusy(bool busy, string message = "")
        {
            if (!this.IsReady) return;
            string json = JsonUtility.ToJson(new BusyEnvelope
            {
                busy = busy,
                message = message ?? string.Empty
            });
            this.m_WebView.EvaluateJS(
                "window.FranklinMenu&&window.FranklinMenu.setBusy(" + json + ");"
            );
        }

        public void SetBusyLocalized(bool busy, string key, string fallback)
        {
            if (!this.IsReady) return;
            string json = JsonUtility.ToJson(new BusyEnvelope
            {
                busy = busy,
                key = key ?? string.Empty,
                message = fallback ?? string.Empty
            });
            this.m_WebView.EvaluateJS(
                "(function(x){var m=window.FranklinMenu,i=window.FranklinMenuI18n;" +
                "if(m){m.setBusy({busy:x.busy,message:i&&i.t&&x.key?" +
                "i.t(x.key,x.message):x.message});}})(" + json + ");"
            );
        }

        public void RequestBack()
        {
            if (!this.IsReady) return;
            this.m_WebView.EvaluateJS(
                "window.FranklinMenu&&window.FranklinMenu.handleBack();"
            );
        }

        private void OnJavaScriptMessage(string json)
        {
            if (this.m_Bridge != null)
            {
                this.m_Bridge.OnWebMessage(json);
            }
        }

        private void OnStateJsonChanged(string json)
        {
            this.m_PendingStateJson = json;
            if (!this.IsReady) return;
            this.ApplyStateToPage(json);
        }

        private void OnBridgeActionReceived(string action, string payload)
        {
            if (!string.Equals(action, "ready", StringComparison.Ordinal)) return;
            this.m_JavascriptReady = true;
            this.TryRevealPage();
        }

        private void OnPageStarted(string url)
        {
            if (IsAllowedLocalPage(url)) return;
            if (this.m_WebView != null)
            {
                this.m_WebView.SetVisibility(false);
            }
            Debug.LogError("Blocked unexpected WebView navigation: " + url, this);
        }

        private void OnPageLoaded(string url)
        {
            if (this.m_WebView == null || this.m_ShuttingDown ||
                !IsAllowedLocalPage(url))
            {
                return;
            }
            bool isFallbackPage = string.Equals(
                url,
                this.m_FallbackUrl,
                StringComparison.OrdinalIgnoreCase
            );
            if (this.m_FallbackRequested && !isFallbackPage) return;

            this.m_PageLoaded = true;
            this.m_IsFallbackPage = isFallbackPage;
            if (isFallbackPage)
            {
                this.TryRevealPage();
                return;
            }

            string adapter = @"
(function () {
  if (!window.Unity || typeof window.Unity.call !== 'function') {
    window.Unity = { call: function (message) { window.location = 'unity:' + message; } };
  }
  window.FranklinUnity = {
    postMessage: function (message) { window.Unity.call(message); }
  };
})();";
            this.m_WebView.EvaluateJS(adapter);
            this.ApplyHostMetricsToPage();
            this.ApplyStateToPage(this.m_PendingStateJson);
            this.TryRevealPage();
        }

        private void OnWebError(string message)
        {
            if (this.m_LogWebErrors)
            {
                Debug.LogError("Franklin WebView error: " + message, this);
            }
            if (!this.m_IsFallbackPage)
            {
                this.LoadFallbackPage();
            }
        }

        private void OnWebHttpError(string message)
        {
            if (this.m_LogWebErrors)
            {
                Debug.LogError("Franklin WebView HTTP error: " + message, this);
            }
            if (!this.m_IsFallbackPage)
            {
                this.LoadFallbackPage();
            }
        }

        private void OnNavigationHooked(string url)
        {
            if (this.m_LogWebErrors)
            {
                Debug.LogWarning("Franklin WebView blocked navigation: " + url, this);
            }
        }

        private void ApplyStateToPage(string json)
        {
            if (this.m_WebView == null || string.IsNullOrWhiteSpace(json)) return;
            this.m_WebView.EvaluateJS(
                "window.FranklinMenu&&window.FranklinMenu.setState(" + json + ");"
            );
        }

        private void LoadFallbackPage()
        {
            if (this.m_WebView == null || this.m_FallbackRequested ||
                string.IsNullOrEmpty(this.m_FallbackUrl))
            {
                return;
            }

            this.m_FallbackRequested = true;
            this.m_PageLoaded = false;
            this.m_IsFallbackPage = false;
            try
            {
                this.m_WebView.SetVisibility(false);
                this.m_WebView.LoadURL(this.m_FallbackUrl);
            }
            catch (Exception exception)
            {
                Debug.LogError("Franklin fallback page could not be loaded: " + exception, this);
                this.DisposeWebView();
            }
        }

        private void TryRevealPage()
        {
            if (!this.isActiveAndEnabled || this.m_ShuttingDown ||
                this.m_WebView == null || !this.m_PageLoaded ||
                (!this.m_IsFallbackPage && !this.m_JavascriptReady))
            {
                return;
            }

            this.m_WebView.SetVisibility(true);
            this.ApplyAudioSuspensionToPage();
        }

        private void ApplyAudioSuspensionToPage(bool forceSuspended = false)
        {
            if (!this.IsReady) return;
            bool suspended = forceSuspended || this.m_ApplicationPaused ||
                !this.m_ApplicationFocused || !this.isActiveAndEnabled;
            this.m_WebView.EvaluateJS(
                "window.FranklinMenu&&window.FranklinMenu.setAudioSuspended(" +
                (suspended ? "true" : "false") + ");"
            );
        }

        private void ApplyHostMetricsToPage()
        {
            if (this.m_WebView == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            bool screenDimensionsChanged =
                this.m_LastScreenWidth >= 0 &&
                (this.m_LastScreenWidth != Screen.width ||
                 this.m_LastScreenHeight != Screen.height);
            this.m_LastScreenWidth = Screen.width;
            this.m_LastScreenHeight = Screen.height;
            this.m_LastSafeArea = safeArea;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (screenDimensionsChanged)
            {
                // GREE caches margin values even though the macOS snapshot size
                // also depends on Screen.width/height. Toggle one harmless pixel
                // only when the Game view changes, then restore true fullscreen.
                this.m_WebView.SetMargins(0, 0, 1, 0);
                this.m_WebView.SetMargins(0, 0, 0, 0);
            }
#endif

            HostMetricsEnvelope metrics = new HostMetricsEnvelope
            {
                safeLeft = Mathf.Clamp01(safeArea.xMin / Screen.width),
                safeRight = Mathf.Clamp01((Screen.width - safeArea.xMax) / Screen.width),
                safeTop = Mathf.Clamp01((Screen.height - safeArea.yMax) / Screen.height),
                safeBottom = Mathf.Clamp01(safeArea.yMin / Screen.height)
            };
            string json = JsonUtility.ToJson(metrics);
            this.m_WebView.EvaluateJS(
                "window.FranklinMenu&&window.FranklinMenu.setHostMetrics(" + json + ");"
            );
        }

        private void DisposeWebView()
        {
            this.m_PageLoaded = false;
            this.m_JavascriptReady = false;
            this.m_IsFallbackPage = false;
            if (this.m_WebView == null) return;

            WebViewObject webView = this.m_WebView;
            this.m_WebView = null;
            webView.SetVisibility(false);
            if (webView.gameObject != null)
            {
                Destroy(webView.gameObject);
            }
        }

        private bool IsAllowedLocalPage(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (string.Equals(url, this.m_IndexUrl, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return !string.IsNullOrEmpty(this.m_LocalBaseUrl) &&
                url.StartsWith(this.m_LocalBaseUrl, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildIndexUrl()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return "file:///android_asset/" + RelativeIndexPath;
#else
            string path = Path.Combine(Application.streamingAssetsPath, RelativeIndexPath);
            return new Uri(path).AbsoluteUri;
#endif
        }

        private static bool IsAllowedAudioCue(string cue)
        {
            switch (cue)
            {
                case "hover":
                case "activate":
                case "back":
                case "panel":
                case "transition":
                case "denied":
                case "error":
                case "notify":
                    return true;
                default:
                    return false;
            }
        }

        [Serializable]
        private sealed class TextEnvelope
        {
            public string key;
            public string value;
            public string tone;
        }

        [Serializable]
        private sealed class BusyEnvelope
        {
            public bool busy;
            public string key;
            public string message;
        }

        [Serializable]
        private sealed class HostMetricsEnvelope
        {
            public float safeLeft;
            public float safeRight;
            public float safeTop;
            public float safeBottom;
        }
    }
}
