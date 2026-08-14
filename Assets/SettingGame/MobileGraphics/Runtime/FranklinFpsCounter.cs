using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FranklinGame.Settings
{
    /// <summary>
    /// Small allocation-conscious FPS readout that automatically attaches to
    /// CanvasPlayerControl (or the best active screen-space Canvas fallback).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(700)]
    [AddComponentMenu("Franklin Game/Settings/FPS Counter")]
    public sealed class FranklinFpsCounter : MonoBehaviour
    {
        private const string CounterObjectName = "Franklin FPS Counter";
        private const string PreferredCanvasName = "CanvasPlayerControl";
        private const float RefreshInterval = 0.5f;
        private const float CanvasRetryInterval = 0.25f;

        private static readonly Color BackgroundColor =
            new(0.015f, 0.02f, 0.025f, 0.78f);
        private static readonly Color GoodColor =
            new(0.2f, 0.95f, 0.62f, 1f);
        private static readonly Color WarningColor =
            new(1f, 0.76f, 0.12f, 1f);
        private static readonly Color SlowColor =
            new(1f, 0.3f, 0.26f, 1f);

        private static FranklinFpsCounter s_Instance;

        private Text m_Label;
        private float m_SampleTime;
        private int m_SampleFrames;
        private float m_NextCanvasRetryTime;
        private bool m_AttachedToCanvas;

        public float CurrentFps { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureSpawned();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureSpawned();
        }

        private static void EnsureSpawned()
        {
            if (s_Instance != null) return;

            FranklinFpsCounter existing =
                FindFirstObjectByType<FranklinFpsCounter>(FindObjectsInactive.Include);
            if (existing != null)
            {
                s_Instance = existing;
                existing.enabled = true;
                return;
            }

            GameObject counterObject = new(
                CounterObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            DontDestroyOnLoad(counterObject);
            counterObject.AddComponent<FranklinFpsCounter>();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            this.gameObject.name = CounterObjectName;
            this.TryAttachToCanvas();
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        private void Update()
        {
            if (!this.m_AttachedToCanvas)
            {
                if (Time.unscaledTime >= this.m_NextCanvasRetryTime)
                {
                    this.m_NextCanvasRetryTime =
                        Time.unscaledTime + CanvasRetryInterval;
                    this.TryAttachToCanvas();
                }
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f) return;

            this.m_SampleTime += deltaTime;
            this.m_SampleFrames += 1;
            if (this.CurrentFps <= 0f && this.m_SampleFrames == 1)
            {
                // Avoid showing a misleading zero during the first half-second.
                this.RefreshLabel(Mathf.Clamp(Mathf.RoundToInt(1f / deltaTime), 0, 999));
            }
            if (this.m_SampleTime < RefreshInterval) return;

            this.CurrentFps = this.m_SampleFrames / this.m_SampleTime;
            this.RefreshLabel(Mathf.Clamp(Mathf.RoundToInt(this.CurrentFps), 0, 999));
            this.m_SampleTime = 0f;
            this.m_SampleFrames = 0;
        }

        private void TryAttachToCanvas()
        {
            Canvas canvas = ResolveTargetCanvas();
            if (canvas == null) return;

            this.transform.SetParent(canvas.transform, false);
            this.gameObject.layer = canvas.gameObject.layer;
            this.BuildVisual();
            this.transform.SetAsLastSibling();
            this.m_AttachedToCanvas = true;
        }

        private void BuildVisual()
        {
            RectTransform rect = this.GetComponent<RectTransform>();
            if (rect == null)
            {
                Debug.LogError("Franklin FPS Counter requires a RectTransform.", this);
                this.m_AttachedToCanvas = false;
                return;
            }
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(126f, 44f);
            rect.localScale = Vector3.one;

            Image background = this.GetComponent<Image>();
            if (background == null) background = this.gameObject.AddComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = false;

            Transform labelTransform = this.transform.Find("Label");
            if (labelTransform == null)
            {
                GameObject labelObject = new(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text)
                );
                labelTransform = labelObject.transform;
                labelTransform.SetParent(this.transform, false);
            }
            labelTransform.gameObject.layer = this.gameObject.layer;

            RectTransform labelRect = labelTransform as RectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 2f);
            labelRect.offsetMax = new Vector2(-8f, -2f);

            this.m_Label = labelTransform.GetComponent<Text>();
            this.m_Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            this.m_Label.fontSize = 24;
            this.m_Label.fontStyle = FontStyle.Bold;
            this.m_Label.alignment = TextAnchor.MiddleCenter;
            this.m_Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            this.m_Label.verticalOverflow = VerticalWrapMode.Truncate;
            this.m_Label.raycastTarget = false;
            this.m_Label.text = "FPS  --";
            this.m_Label.color = WarningColor;
        }

        private void RefreshLabel(int fps)
        {
            if (this.m_Label == null) return;
            this.m_Label.text = $"FPS  {fps}";
            this.m_Label.color = fps >= 55
                ? GoodColor
                : fps >= 30
                    ? WarningColor
                    : SlowColor;
        }

        private static Canvas ResolveTargetCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            Canvas bestFallback = null;
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                if (canvas.gameObject.name == PreferredCanvasName) return canvas;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;

                if (bestFallback == null || canvas.sortingOrder > bestFallback.sortingOrder)
                {
                    bestFallback = canvas;
                }
            }

            return bestFallback;
        }
    }
}
