using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace FranklinGame.PhoneSystem
{
    /// <summary>
    /// Runtime video application for the simulated phone. Frames from the
    /// existing selfie RenderTexture are stored in a compact Franklin clip
    /// container so recording and playback work in builds without an external
    /// native MP4 encoder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FranklinPhoneVideoRecorder : MonoBehaviour
    {
        private const string VIDEO_FOLDER = "FranklinPhoneVideos";
        private const string VIDEO_EXTENSION = ".fvideo";
        private const string VIDEO_APP_NAME = "Video App";
        private const string VIDEO_BUTTON_NAME = "App Video";
        private const int HEADER_FPS_OFFSET = 16;
        private const int PHOTO_COLUMNS = 3;
        private const float PHOTO_VIEWPORT_HEIGHT = 696f;
        private const float PHOTO_SLOT_WIDTH = 118f;
        private const float PHOTO_SLOT_HEIGHT = 193f;
        private const float PHOTO_COLUMN_STEP = 137f;
        private const float PHOTO_ROW_STEP = 218f;
        private const float PHOTO_TOP_SLOT_CENTER = 130f;
        private static readonly byte[] FILE_MAGIC =
            Encoding.ASCII.GetBytes("FRVID001");

        [Header("Runtime Video Capture")]
        [Tooltip("Width of each stored video frame. The default keeps recording light enough for mobile builds.")]
        [SerializeField, Range(180, 720)] private int m_CaptureWidth = 350;
        [Tooltip("Height of each stored video frame. Keep the same portrait aspect as the Camera app.")]
        [SerializeField, Range(300, 1280)] private int m_CaptureHeight = 588;
        [Tooltip("Target recording rate. The simulated phone records at 30 FPS by default.")]
        [SerializeField, Range(8, 30)] private int m_FramesPerSecond = 30;
        [Tooltip("Mobile-oriented JPEG quality chosen to keep 30 FPS encoding and playback smooth.")]
        [SerializeField, Range(35, 95)] private int m_JpegQuality = 55;
        [SerializeField, Range(5f, 120f)] private float m_MaxDuration = 30f;
        [SerializeField, Range(1, 30)] private int m_MaxStoredClips = 12;
        [SerializeField] private FilterMode m_FilterMode = FilterMode.Bilinear;

        private FranklinPhoneSystem m_PhoneSystem;
        private FranklinPhoneSelfieCamera m_SelfieCamera;
        private GameObject m_AppScreen;
        private GameObject m_PhotosApp;
        private Button m_AppButton;
        private RawImage m_LiveFeed;
        private Text m_LiveStatus;
        private Text m_RecordTimer;
        private Button m_RecordButton;
        private Image m_RecordGlyph;
        private Text m_RecordHint;
        private Button m_CameraModeButton;
        private Text m_CameraModeLabel;
        private GameObject m_Library;
        private RectTransform m_LibraryContent;
        private Text m_EmptyLibraryText;
        private GameObject m_Viewer;
        private RawImage m_ViewerImage;
        private Text m_ViewerInfo;
        private Text m_PlayLabel;
        private Text m_DeleteLabel;
        private Font m_Font;
        private Sprite m_RoundedSprite;
        private Sprite m_CircleSprite;
        private bool m_Initialized;
        private bool m_IsAppActive;
        private bool m_IsRecording;
        private float m_RecordStartedAt;
        private float m_LastHeaderRefresh;
        private Coroutine m_RecordRoutine;
        private BinaryWriter m_RecordWriter;
        private string m_TemporaryPath;
        private string m_FinalPath;
        private int m_RecordedFrameCount;
        private byte[] m_FirstFrameJpeg;
        private Texture2D m_CaptureTexture;
        private readonly List<Texture2D> m_ThumbnailTextures = new();
        private readonly List<Texture2D> m_PhotosVideoTextures = new();
        private readonly List<GameObject> m_PhotosVideoSlots = new();
        private readonly List<ClipInfo> m_Clips = new();

        private readonly List<byte[]> m_PlaybackFrames = new();
        private Texture2D m_PlaybackTexture;
        private ClipInfo m_SelectedClip;
        private bool m_IsPlaying;
        private bool m_DeleteArmed;
        private float m_PlaybackTime;
        private int m_DisplayedFrame = -1;

        private sealed class ClipInfo
        {
            public string Path;
            public string ThumbnailPath;
            public int Width;
            public int Height;
            public int Fps;
            public int FrameCount;
            public double Duration;
            public DateTime Created;
        }

        public bool IsRecording => this.m_IsRecording;
        public int StoredClipCount => this.GetVideoPaths().Length;
        public string VideoDirectory => Path.Combine(
            Application.persistentDataPath,
            VIDEO_FOLDER
        );

        public string HeaderStatus => this.m_IsRecording
            ? $"REC • {FormatTime(Time.unscaledTime - this.m_RecordStartedAt)}"
            : $"{this.StoredClipCount} SAVED CLIPS";

        public void Initialize(
            FranklinPhoneSystem phoneSystem,
            FranklinPhoneSelfieCamera selfieCamera,
            GameObject homeScreen,
            GameObject[] sourceScreens,
            Button[] sourceButtons,
            out GameObject[] appScreens,
            out Button[] appButtons)
        {
            this.m_PhoneSystem = phoneSystem;
            this.m_SelfieCamera = selfieCamera;
            appScreens = sourceScreens ?? Array.Empty<GameObject>();
            appButtons = sourceButtons ?? Array.Empty<Button>();
            if (this.m_Initialized) return;
            if (homeScreen == null || appScreens.Length < 5 ||
                appScreens[4] == null || appButtons.Length < 5 ||
                appButtons[4] == null)
            {
                Debug.LogWarning(
                    "Franklin Phone Video could not find the Camera app template."
                );
                return;
            }

            this.ResolveVisualAssets(appScreens[4]);
            this.m_PhotosApp = appScreens.Length > 5 ? appScreens[5] : null;
            this.m_AppButton = this.CreateVideoHomeButton(
                homeScreen.transform,
                appButtons[4]
            );
            this.m_AppScreen = this.BuildVideoApplication(
                appScreens[4].transform.parent
            );
            this.m_AppScreen.SetActive(false);

            appScreens = Append(appScreens, this.m_AppScreen);
            appButtons = Append(appButtons, this.m_AppButton);
            this.LayoutHomeApplications(appButtons, homeScreen.transform);
            this.m_Initialized = true;
        }

        public void SetVideoAppActive(bool active)
        {
            if (!this.m_Initialized) return;
            if (this.m_IsAppActive == active)
            {
                if (active) this.SyncLiveTexture();
                return;
            }

            this.m_IsAppActive = active;
            if (active)
            {
                this.m_SelfieCamera?.SetBackCameraMode(false);
                this.CloseViewer(false);
                if (this.m_Library != null) this.m_Library.SetActive(false);
                this.SyncLiveTexture();
                this.RefreshCameraModeUi();
                this.SetLiveState("● SELFIE", "TAP TO RECORD");
                this.RefreshTimer(0f);
            }
            else
            {
                if (this.m_IsRecording) this.StopRecording(true);
                this.CloseViewer(false);
                if (this.m_Library != null) this.m_Library.SetActive(false);
                this.m_SelfieCamera?.SetBackCameraMode(false);
                this.RefreshCameraModeUi();
            }
        }

        private void Update()
        {
            if (!this.m_IsAppActive) return;
            this.SyncLiveTexture();

            if (this.m_IsRecording)
            {
                float elapsed = Time.unscaledTime - this.m_RecordStartedAt;
                this.RefreshTimer(elapsed);
                if (Time.unscaledTime >= this.m_LastHeaderRefresh)
                {
                    this.m_LastHeaderRefresh = Time.unscaledTime + 0.2f;
                    this.m_PhoneSystem?.RefreshVideoHeader();
                }
                if (elapsed >= this.m_MaxDuration) this.StopRecording(true);
            }

            if (this.m_IsPlaying) this.UpdatePlayback();
        }

        private void OnDisable()
        {
            if (this.m_IsRecording) this.StopRecording(true);
            this.ClosePlaybackStream();
        }

        private void OnDestroy()
        {
            if (this.m_IsRecording) this.StopRecording(true);
            this.ClosePlaybackStream();
            this.DestroyThumbnailTextures();
            this.DestroyPhotosVideoSlots();
            if (this.m_CaptureTexture != null) Destroy(this.m_CaptureTexture);
            if (this.m_PlaybackTexture != null) Destroy(this.m_PlaybackTexture);
        }

        private void ToggleRecording()
        {
            if (this.m_IsRecording) this.StopRecording(true);
            else this.StartRecording();
        }

        private void StartRecording()
        {
            RenderTexture source = this.m_SelfieCamera?.LiveTexture;
            if (!this.m_IsAppActive || source == null || !source.IsCreated())
            {
                this.SetLiveState("CAMERA NOT READY", "TRY AGAIN");
                return;
            }

            try
            {
                Directory.CreateDirectory(this.VideoDirectory);
                string id = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") +
                    "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                this.m_FinalPath = Path.Combine(
                    this.VideoDirectory,
                    "VID_" + id + VIDEO_EXTENSION
                );
                this.m_TemporaryPath = this.m_FinalPath + ".tmp";
                FileStream stream = new(
                    this.m_TemporaryPath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                this.m_RecordWriter = new BinaryWriter(stream);
                this.WriteHeader(this.m_RecordWriter);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not start phone video recording: " + exception.Message);
                this.CloseRecordWriter();
                this.SetLiveState("STORAGE ERROR", "NOT RECORDED");
                return;
            }

            this.m_RecordedFrameCount = 0;
            this.m_FirstFrameJpeg = null;
            this.m_RecordStartedAt = Time.unscaledTime;
            this.m_LastHeaderRefresh = 0f;
            this.m_IsRecording = true;
            this.SetRecordButtonRecording(true);
            if (this.m_CameraModeButton != null)
                this.m_CameraModeButton.interactable = false;
            this.SetLiveState("● REC", "TAP TO STOP");
            this.RefreshTimer(0f);
            this.m_RecordRoutine = this.StartCoroutine(this.RecordFrames());
            this.m_PhoneSystem?.RefreshVideoHeader();
        }

        private IEnumerator RecordFrames()
        {
            WaitForEndOfFrame endOfFrame = new();
            float interval = 1f / Mathf.Max(1, this.m_FramesPerSecond);
            float nextCapture = Time.unscaledTime;

            while (this.m_IsRecording)
            {
                yield return endOfFrame;
                if (!this.m_IsRecording) yield break;
                if (Time.unscaledTime + 0.001f < nextCapture) continue;
                nextCapture = Mathf.Max(
                    nextCapture + interval,
                    Time.unscaledTime + interval * 0.25f
                );
                this.CaptureFrame();
            }
        }

        private void CaptureFrame()
        {
            RenderTexture source = this.m_SelfieCamera?.LiveTexture;
            if (source == null || !source.IsCreated() ||
                this.m_RecordWriter == null)
            {
                return;
            }

            RenderTexture scaled = RenderTexture.GetTemporary(
                this.m_CaptureWidth,
                this.m_CaptureHeight,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default
            );
            scaled.filterMode = this.m_FilterMode;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, scaled);
                RenderTexture.active = scaled;
                if (this.m_CaptureTexture == null ||
                    this.m_CaptureTexture.width != this.m_CaptureWidth ||
                    this.m_CaptureTexture.height != this.m_CaptureHeight)
                {
                    if (this.m_CaptureTexture != null)
                        Destroy(this.m_CaptureTexture);
                    this.m_CaptureTexture = new Texture2D(
                        this.m_CaptureWidth,
                        this.m_CaptureHeight,
                        TextureFormat.RGB24,
                        false
                    );
                    this.m_CaptureTexture.filterMode = this.m_FilterMode;
                }

                this.m_CaptureTexture.ReadPixels(
                    new Rect(0f, 0f, this.m_CaptureWidth, this.m_CaptureHeight),
                    0,
                    0,
                    false
                );
                this.m_CaptureTexture.Apply(false, false);
                byte[] jpeg = this.m_CaptureTexture.EncodeToJPG(this.m_JpegQuality);
                if (jpeg == null || jpeg.Length == 0) return;
                this.m_RecordWriter.Write(jpeg.Length);
                this.m_RecordWriter.Write(jpeg);
                if (this.m_FirstFrameJpeg == null)
                    this.m_FirstFrameJpeg = jpeg;
                this.m_RecordedFrameCount++;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Phone video frame capture failed: " + exception.Message);
                this.StopRecording(false);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(scaled);
            }
        }

        private void StopRecording(bool save)
        {
            if (!this.m_IsRecording && this.m_RecordWriter == null) return;
            this.m_IsRecording = false;
            if (this.m_RecordRoutine != null)
            {
                this.StopCoroutine(this.m_RecordRoutine);
                this.m_RecordRoutine = null;
            }

            bool saved = false;
            try
            {
                if (this.m_RecordWriter != null)
                {
                    float elapsed = Mathf.Max(
                        0.001f,
                        Time.unscaledTime - this.m_RecordStartedAt
                    );
                    int playbackFps = Mathf.Clamp(
                        Mathf.RoundToInt(this.m_RecordedFrameCount / elapsed),
                        1,
                        Mathf.Max(1, this.m_FramesPerSecond)
                    );
                    double duration = this.m_RecordedFrameCount /
                        (double)playbackFps;
                    this.m_RecordWriter.Flush();
                    this.m_RecordWriter.BaseStream.Seek(
                        HEADER_FPS_OFFSET,
                        SeekOrigin.Begin
                    );
                    this.m_RecordWriter.Write(playbackFps);
                    this.m_RecordWriter.Write(this.m_RecordedFrameCount);
                    this.m_RecordWriter.Write(duration);
                    this.m_RecordWriter.Flush();
                }
                this.CloseRecordWriter();

                if (save && this.m_RecordedFrameCount > 0 &&
                    !string.IsNullOrEmpty(this.m_TemporaryPath) &&
                    File.Exists(this.m_TemporaryPath))
                {
                    File.Move(this.m_TemporaryPath, this.m_FinalPath);
                    if (this.m_FirstFrameJpeg != null)
                        File.WriteAllBytes(
                            this.m_FinalPath + ".jpg",
                            this.m_FirstFrameJpeg
                        );
                    saved = true;
                    this.TrimStoredClips();
                    this.RefreshPhotosMedia();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not save phone video: " + exception.Message);
            }
            finally
            {
                this.CloseRecordWriter();
                if (!saved && !string.IsNullOrEmpty(this.m_TemporaryPath))
                    TryDelete(this.m_TemporaryPath);
                this.m_TemporaryPath = null;
                this.m_FinalPath = null;
                this.m_FirstFrameJpeg = null;
            }

            this.SetRecordButtonRecording(false);
            if (this.m_CameraModeButton != null)
                this.m_CameraModeButton.interactable = true;
            this.RefreshTimer(0f);
            this.SetLiveState(
                saved ? "VIDEO SAVED" : "RECORDING CANCELED",
                "TAP TO RECORD"
            );
            this.m_PhoneSystem?.RefreshVideoHeader();
        }

        private void WriteHeader(BinaryWriter writer)
        {
            writer.Write(FILE_MAGIC);
            writer.Write(this.m_CaptureWidth);
            writer.Write(this.m_CaptureHeight);
            writer.Write(this.m_FramesPerSecond);
            writer.Write(0);
            writer.Write(0d);
        }

        private void CloseRecordWriter()
        {
            if (this.m_RecordWriter == null) return;
            try { this.m_RecordWriter.Dispose(); }
            catch (Exception) { /* Best effort during shutdown. */ }
            this.m_RecordWriter = null;
        }

        private void OpenLibrary()
        {
            if (this.m_IsRecording) this.StopRecording(true);
            // Browsing saved media must not keep the live GC2 selfie shot,
            // Player Facing or raised selfie pose active in the world.
            this.m_SelfieCamera?.SetSelfieActive(false);
            this.CloseViewer(false);
            this.RefreshLibrary();
            if (this.m_Library != null) this.m_Library.SetActive(true);
            this.m_PhoneSystem?.RefreshVideoHeader();
        }

        /// <summary>
        /// Appends saved video thumbnails after the captured photos so the
        /// Photos application acts as the phone's shared media library.
        /// </summary>
        public void RefreshPhotosMedia()
        {
            this.DestroyPhotosVideoSlots();
            if (this.m_PhotosApp == null) return;

            RectTransform content = FindChildRecursive(
                this.m_PhotosApp.transform,
                "Content"
            ) as RectTransform;
            if (content == null) return;

            List<ClipInfo> clips = new();
            foreach (string path in this.GetVideoPaths())
                if (TryReadClip(path, out ClipInfo clip)) clips.Add(clip);
            clips.Sort((left, right) => right.Created.CompareTo(left.Created));

            int photoCount = this.m_SelfieCamera != null
                ? this.m_SelfieCamera.StoredPhotoCount
                : 0;
            Image photoTemplate = FindChildRecursive(
                this.m_PhotosApp.transform,
                "Photo 1"
            )?.GetComponent<Image>();

            for (int i = 0; i < clips.Count; ++i)
            {
                ClipInfo clip = clips[i];
                int mediaIndex = photoCount + i;
                int column = mediaIndex % PHOTO_COLUMNS;
                int row = mediaIndex / PHOTO_COLUMNS;
                Image slot = CreateImage(
                    "Saved Video " + (i + 1),
                    content,
                    new Vector2(
                        -PHOTO_COLUMN_STEP + column * PHOTO_COLUMN_STEP,
                        -PHOTO_TOP_SLOT_CENTER - row * PHOTO_ROW_STEP
                    ),
                    new Vector2(PHOTO_SLOT_WIDTH, PHOTO_SLOT_HEIGHT),
                    new Color(0.015f, 0.022f, 0.030f, 1f),
                    photoTemplate != null
                        ? photoTemplate.sprite
                        : this.m_RoundedSprite,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 0.5f)
                );
                if (photoTemplate != null) slot.type = photoTemplate.type;
                slot.raycastTarget = true;
                Button button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slot;
                button.onClick.AddListener(this.m_PhoneSystem.NotifyRuntimeUiTap);
                button.onClick.AddListener(() => this.OpenClipFromPhotos(clip));

                RawImage thumbnail = CreateRawImage(
                    "Video Thumbnail",
                    slot.transform,
                    Vector2.zero,
                    new Vector2(PHOTO_SLOT_WIDTH, PHOTO_SLOT_HEIGHT)
                );
                thumbnail.raycastTarget = false;
                Texture2D texture = this.LoadPhotosVideoThumbnail(
                    clip.ThumbnailPath
                );
                thumbnail.texture = texture;
                thumbnail.transform.SetAsFirstSibling();

                Image videoBadge = CreateImage(
                    "Video Badge",
                    slot.transform,
                    new Vector2(0f, -75f),
                    new Vector2(98f, 28f),
                    new Color(0.58f, 0.035f, 0.055f, 0.94f),
                    this.m_RoundedSprite
                );
                Text videoLabel = CreateText(
                    "Video Label",
                    videoBadge.transform,
                    Vector2.zero,
                    new Vector2(94f, 26f),
                    11,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold,
                    Color.white
                );
                videoLabel.text = "▶ " + FormatTime((float)clip.Duration);
                this.m_PhotosVideoSlots.Add(slot.gameObject);
            }

            int totalCount = photoCount + clips.Count;
            int rows = Mathf.Max(
                1,
                Mathf.CeilToInt(totalCount / (float)PHOTO_COLUMNS)
            );
            float contentHeight = Mathf.Max(
                PHOTO_VIEWPORT_HEIGHT,
                PHOTO_TOP_SLOT_CENTER + PHOTO_SLOT_HEIGHT * 0.5f +
                (rows - 1) * PHOTO_ROW_STEP + 33.5f
            );
            content.sizeDelta = new Vector2(0f, contentHeight);

            ScrollRect scroll = FindChildRecursive(
                this.m_PhotosApp.transform,
                "Photos Scroll Area"
            )?.GetComponent<ScrollRect>();
            if (scroll != null)
                scroll.vertical = contentHeight > PHOTO_VIEWPORT_HEIGHT + 0.5f;

            Transform empty = FindChildRecursive(
                this.m_PhotosApp.transform,
                "Photo Empty State"
            );
            if (empty != null)
            {
                empty.gameObject.SetActive(totalCount == 0);
                empty.SetAsLastSibling();
            }
        }

        private void OpenClipFromPhotos(ClipInfo clip)
        {
            this.m_PhoneSystem?.OpenApp(6);
            this.OpenClip(clip);
        }

        private Texture2D LoadPhotosVideoThumbnail(string path)
        {
            Texture2D texture = new(2, 2, TextureFormat.RGB24, false);
            texture.filterMode = this.m_FilterMode;
            try
            {
                if (File.Exists(path) &&
                    texture.LoadImage(File.ReadAllBytes(path), false))
                {
                    this.m_PhotosVideoTextures.Add(texture);
                    return texture;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Could not load Photos video thumbnail: " +
                    exception.Message
                );
            }
            Destroy(texture);
            return Texture2D.blackTexture;
        }

        private void CloseLibrary()
        {
            if (this.m_Library != null) this.m_Library.SetActive(false);
            if (this.m_IsAppActive)
                this.m_SelfieCamera?.SetSelfieActive(true);
            this.RefreshCameraModeUi();
            this.SetLiveState(
                this.m_SelfieCamera != null &&
                this.m_SelfieCamera.IsBackCameraMode
                    ? "● BACK"
                    : "● SELFIE",
                "TAP TO RECORD"
            );
        }

        private void ToggleCameraMode()
        {
            if (this.m_IsRecording || this.m_SelfieCamera == null) return;
            bool backCamera = !this.m_SelfieCamera.IsBackCameraMode;
            this.m_SelfieCamera.SetBackCameraMode(backCamera);
            this.RefreshCameraModeUi();
            this.SetLiveState(
                backCamera ? "● BACK" : "● SELFIE",
                "TAP TO RECORD"
            );
        }

        private void RefreshCameraModeUi()
        {
            if (this.m_CameraModeLabel == null) return;
            bool backCamera = this.m_SelfieCamera != null &&
                this.m_SelfieCamera.IsBackCameraMode;
            this.m_CameraModeLabel.text = "↻";
            this.m_CameraModeLabel.fontSize = 30;
            this.m_CameraModeLabel.fontStyle = FontStyle.Bold;
            this.m_CameraModeLabel.color = backCamera
                ? new Color(1f, 0.32f, 0.34f, 1f)
                : new Color(0.30f, 0.92f, 0.90f, 1f);
            this.m_CameraModeLabel.rectTransform.localEulerAngles = backCamera
                ? new Vector3(0f, 0f, 180f)
                : Vector3.zero;
        }

        private void RefreshLibrary()
        {
            this.DestroyThumbnailTextures();
            this.m_Clips.Clear();
            if (this.m_LibraryContent == null) return;
            for (int i = this.m_LibraryContent.childCount - 1; i >= 0; --i)
                Destroy(this.m_LibraryContent.GetChild(i).gameObject);

            foreach (string path in this.GetVideoPaths())
                if (TryReadClip(path, out ClipInfo clip))
                    this.m_Clips.Add(clip);

            this.m_Clips.Sort((left, right) => right.Created.CompareTo(left.Created));
            if (this.m_EmptyLibraryText != null)
                this.m_EmptyLibraryText.gameObject.SetActive(this.m_Clips.Count == 0);

            const float cardWidth = 184f;
            const float cardHeight = 230f;
            const float columnStep = 198f;
            const float rowStep = 244f;
            int rows = Mathf.CeilToInt(this.m_Clips.Count / 2f);
            this.m_LibraryContent.sizeDelta = new Vector2(
                0f,
                Mathf.Max(596f, rows * rowStep + 8f)
            );

            for (int i = 0; i < this.m_Clips.Count; ++i)
            {
                ClipInfo clip = this.m_Clips[i];
                int column = i % 2;
                int row = i / 2;
                Image card = CreateImage(
                    "Clip " + (i + 1),
                    this.m_LibraryContent,
                    new Vector2(-99f + column * columnStep, -8f - row * rowStep),
                    new Vector2(cardWidth, cardHeight),
                    new Color(0.035f, 0.055f, 0.07f, 1f),
                    this.m_RoundedSprite,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f)
                );
                card.raycastTarget = true;
                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                button.onClick.AddListener(this.m_PhoneSystem.NotifyRuntimeUiTap);
                button.onClick.AddListener(() => this.OpenClip(clip));

                RawImage thumbnail = CreateRawImage(
                    "Thumbnail",
                    card.transform,
                    new Vector2(0f, 17f),
                    new Vector2(174f, 185f)
                );
                thumbnail.raycastTarget = false;
                thumbnail.texture = this.LoadThumbnail(clip.ThumbnailPath);
                thumbnail.uvRect = new Rect(0f, 0f, 1f, 1f);
                Text duration = CreateText(
                    "Duration",
                    card.transform,
                    new Vector2(0f, -91f),
                    new Vector2(166f, 25f),
                    14,
                    TextAnchor.MiddleLeft,
                    FontStyle.Bold,
                    Color.white
                );
                duration.text = "▶ " + FormatTime((float)clip.Duration);
                Text date = CreateText(
                    "Date",
                    card.transform,
                    new Vector2(0f, -108f),
                    new Vector2(166f, 20f),
                    11,
                    TextAnchor.MiddleLeft,
                    FontStyle.Normal,
                    new Color(0.52f, 0.65f, 0.68f, 1f)
                );
                date.text = clip.Created.ToString("dd/MM • HH:mm");
            }
        }

        private Texture2D LoadThumbnail(string path)
        {
            Texture2D texture = new(2, 2, TextureFormat.RGB24, false);
            texture.filterMode = this.m_FilterMode;
            try
            {
                if (File.Exists(path) && texture.LoadImage(File.ReadAllBytes(path), false))
                {
                    this.m_ThumbnailTextures.Add(texture);
                    return texture;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not load video thumbnail: " + exception.Message);
            }
            Destroy(texture);
            return Texture2D.blackTexture;
        }

        private void OpenClip(ClipInfo clip)
        {
            // Playback is independent from the live RenderTexture. Release the
            // phone Camera Shot so watching a saved clip never makes the Player
            // perform the selfie pose in the current game world.
            this.m_SelfieCamera?.SetSelfieActive(false);
            this.ClosePlaybackStream();
            this.m_SelectedClip = clip;
            this.m_DeleteArmed = false;
            if (this.m_DeleteLabel != null) this.m_DeleteLabel.text = "DELETE";

            try
            {
                using FileStream stream = new(
                    clip.Path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );
                using BinaryReader reader = new(stream);
                this.ReadAndValidateHeader(reader);
                for (int i = 0; i < clip.FrameCount; ++i)
                {
                    int length = reader.ReadInt32();
                    if (length <= 0 ||
                        stream.Position + length > stream.Length)
                    {
                        throw new InvalidDataException("Invalid video frame data.");
                    }
                    byte[] frame = reader.ReadBytes(length);
                    if (frame.Length != length)
                        throw new EndOfStreamException("Incomplete video frame.");
                    this.m_PlaybackFrames.Add(frame);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not open phone video: " + exception.Message);
                this.ClosePlaybackStream();
                return;
            }

            if (this.m_Library != null) this.m_Library.SetActive(false);
            if (this.m_Viewer != null) this.m_Viewer.SetActive(true);
            this.m_PlaybackTime = 0f;
            this.m_DisplayedFrame = -1;
            this.m_IsPlaying = this.m_PlaybackFrames.Count > 0;
            this.ShowPlaybackFrame(0);
            this.RefreshPlaybackControls();
        }

        private void UpdatePlayback()
        {
            if (this.m_SelectedClip == null ||
                this.m_PlaybackFrames.Count == 0)
            {
                this.m_IsPlaying = false;
                return;
            }

            this.m_PlaybackTime += Time.unscaledDeltaTime;
            float duration = Mathf.Max(0.01f, (float)this.m_SelectedClip.Duration);
            if (this.m_PlaybackTime >= duration) this.m_PlaybackTime %= duration;
            int frame = Mathf.Clamp(
                Mathf.FloorToInt(this.m_PlaybackTime * this.m_SelectedClip.Fps),
                0,
                this.m_PlaybackFrames.Count - 1
            );
            this.ShowPlaybackFrame(frame);
            this.RefreshViewerInfo();
        }

        private void ShowPlaybackFrame(int frameIndex)
        {
            if (frameIndex == this.m_DisplayedFrame ||
                frameIndex < 0 || frameIndex >= this.m_PlaybackFrames.Count)
            {
                return;
            }

            try
            {
                if (this.m_PlaybackTexture == null)
                {
                    this.m_PlaybackTexture = new Texture2D(
                        2,
                        2,
                        TextureFormat.RGB24,
                        false
                    );
                    this.m_PlaybackTexture.filterMode = this.m_FilterMode;
                }
                if (this.m_PlaybackTexture.LoadImage(
                    this.m_PlaybackFrames[frameIndex],
                    false
                ))
                {
                    this.m_ViewerImage.texture = this.m_PlaybackTexture;
                    this.m_DisplayedFrame = frameIndex;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Phone video playback failed: " + exception.Message);
                this.m_IsPlaying = false;
                this.RefreshPlaybackControls();
            }
        }

        private void TogglePlayback()
        {
            if (this.m_PlaybackFrames.Count == 0) return;
            this.m_IsPlaying = !this.m_IsPlaying;
            this.RefreshPlaybackControls();
        }

        private void CloseViewer()
        {
            this.CloseViewer(true);
        }

        private void CloseViewer(bool returnToLibrary)
        {
            this.ClosePlaybackStream();
            this.m_SelectedClip = null;
            this.m_IsPlaying = false;
            this.m_DeleteArmed = false;
            if (this.m_ViewerImage != null) this.m_ViewerImage.texture = null;
            if (this.m_Viewer != null) this.m_Viewer.SetActive(false);
            if (returnToLibrary && this.m_IsAppActive)
            {
                this.RefreshLibrary();
                if (this.m_Library != null) this.m_Library.SetActive(true);
            }
        }

        private void DeleteSelectedClip()
        {
            if (this.m_SelectedClip == null) return;
            if (!this.m_DeleteArmed)
            {
                this.m_DeleteArmed = true;
                if (this.m_DeleteLabel != null)
                    this.m_DeleteLabel.text = "CONFIRM";
                return;
            }

            string path = this.m_SelectedClip.Path;
            string thumbnail = this.m_SelectedClip.ThumbnailPath;
            this.ClosePlaybackStream();
            TryDelete(path);
            TryDelete(thumbnail);
            this.m_SelectedClip = null;
            this.CloseViewer(true);
            this.RefreshPhotosMedia();
            this.m_PhoneSystem?.RefreshVideoHeader();
        }

        private void ClosePlaybackStream()
        {
            this.m_PlaybackFrames.Clear();
            this.m_DisplayedFrame = -1;
        }

        private void RefreshPlaybackControls()
        {
            if (this.m_PlayLabel != null)
                this.m_PlayLabel.text = this.m_IsPlaying ? "PAUSE" : "PLAY";
            this.RefreshViewerInfo();
        }

        private void RefreshViewerInfo()
        {
            if (this.m_ViewerInfo == null || this.m_SelectedClip == null) return;
            this.m_ViewerInfo.text =
                $"{FormatTime(this.m_PlaybackTime)} / " +
                FormatTime((float)this.m_SelectedClip.Duration);
        }

        private static bool TryReadClip(string path, out ClipInfo clip)
        {
            clip = null;
            try
            {
                using FileStream stream = new(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite
                );
                using BinaryReader reader = new(stream);
                ReadAndValidateHeaderStatic(
                    reader,
                    out int width,
                    out int height,
                    out int fps,
                    out int frameCount,
                    out double duration
                );
                clip = new ClipInfo
                {
                    Path = path,
                    ThumbnailPath = path + ".jpg",
                    Width = width,
                    Height = height,
                    Fps = fps,
                    FrameCount = frameCount,
                    Duration = duration,
                    Created = File.GetLastWriteTime(path)
                };
                return frameCount > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ReadAndValidateHeader(BinaryReader reader)
        {
            ReadAndValidateHeaderStatic(
                reader,
                out _,
                out _,
                out _,
                out _,
                out _
            );
        }

        private static void ReadAndValidateHeaderStatic(
            BinaryReader reader,
            out int width,
            out int height,
            out int fps,
            out int frameCount,
            out double duration)
        {
            byte[] magic = reader.ReadBytes(FILE_MAGIC.Length);
            if (!magic.SequenceEqual(FILE_MAGIC))
                throw new InvalidDataException("Unknown Franklin video format.");
            width = reader.ReadInt32();
            height = reader.ReadInt32();
            fps = reader.ReadInt32();
            frameCount = reader.ReadInt32();
            duration = reader.ReadDouble();
            if (width <= 0 || height <= 0 || fps <= 0 || frameCount < 0 ||
                duration < 0d)
            {
                throw new InvalidDataException("Invalid Franklin video header.");
            }
        }

        private string[] GetVideoPaths()
        {
            try
            {
                if (!Directory.Exists(this.VideoDirectory))
                    return Array.Empty<string>();
                return Directory.GetFiles(
                    this.VideoDirectory,
                    "*" + VIDEO_EXTENSION,
                    SearchOption.TopDirectoryOnly
                );
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        private void TrimStoredClips()
        {
            string[] paths = this.GetVideoPaths()
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
            for (int i = Mathf.Max(1, this.m_MaxStoredClips); i < paths.Length; ++i)
            {
                TryDelete(paths[i]);
                TryDelete(paths[i] + ".jpg");
            }
        }

        private void SyncLiveTexture()
        {
            if (this.m_LiveFeed == null) return;
            RenderTexture texture = this.m_SelfieCamera?.LiveTexture;
            if (texture != null && this.m_LiveFeed.texture != texture)
                this.m_LiveFeed.texture = texture;
        }

        private void SetLiveState(string status, string hint)
        {
            if (this.m_LiveStatus != null) this.m_LiveStatus.text = status;
            if (this.m_RecordHint != null) this.m_RecordHint.text = hint;
        }

        private void RefreshTimer(float seconds)
        {
            if (this.m_RecordTimer != null)
                this.m_RecordTimer.text = FormatTime(seconds);
        }

        private void SetRecordButtonRecording(bool recording)
        {
            if (this.m_RecordGlyph == null) return;
            this.m_RecordGlyph.rectTransform.sizeDelta = recording
                ? new Vector2(34f, 34f)
                : new Vector2(56f, 56f);
            this.m_RecordGlyph.sprite = recording
                ? this.m_RoundedSprite
                : this.m_CircleSprite;
            this.m_RecordGlyph.color = new Color(0.96f, 0.12f, 0.15f, 1f);
        }

        private void ResolveVisualAssets(GameObject cameraScreen)
        {
            this.m_RoundedSprite = cameraScreen.GetComponent<Image>()?.sprite;
            Transform shutter = FindChildRecursive(cameraScreen.transform, "Shutter");
            this.m_CircleSprite = shutter != null
                ? shutter.GetComponent<Image>()?.sprite
                : this.m_RoundedSprite;
            Text templateText = this.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.font != null);
            this.m_Font = templateText != null ? templateText.font : null;
        }

        private Button CreateVideoHomeButton(Transform home, Button cameraButton)
        {
            Transform existing = home.Find(VIDEO_BUTTON_NAME);
            if (existing != null)
            {
                Button existingButton = existing.GetComponentInChildren<Button>(true);
                if (existingButton != null) return existingButton;
            }

            RectTransform cameraCell = cameraButton.transform.parent as RectTransform;
            RectTransform cell = Instantiate(cameraCell, home);
            cell.name = VIDEO_BUTTON_NAME;
            Button button = cell.GetComponentInChildren<Button>(true);
            button.onClick.RemoveAllListeners();
            Image icon = button.targetGraphic as Image;
            if (icon != null)
                icon.color = new Color(1f, 0.34f, 0.36f, 1f);
            Text label = cell.Find("Label")?.GetComponent<Text>();
            if (label != null) label.text = "Video";

            Image badge = CreateImage(
                "REC Badge",
                button.transform,
                new Vector2(34f, 35f),
                new Vector2(34f, 20f),
                new Color(0.68f, 0.04f, 0.05f, 1f),
                this.m_RoundedSprite
            );
            Text rec = CreateText(
                "REC",
                badge.transform,
                Vector2.zero,
                new Vector2(32f, 18f),
                9,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                Color.white
            );
            rec.text = "REC";
            return button;
        }

        private void LayoutHomeApplications(Button[] buttons, Transform home)
        {
            RectTransform homeRect = home as RectTransform;
            if (homeRect != null)
            {
                Vector2 center = new(0.5f, 0.5f);
                homeRect.anchorMin = center;
                homeRect.anchorMax = center;
                homeRect.pivot = center;
                homeRect.anchoredPosition = new Vector2(0f, -66f);
                homeRect.sizeDelta = new Vector2(425f, 714f);
            }

            for (int i = 0; i < buttons.Length; ++i)
            {
                if (buttons[i] == null || buttons[i].transform.parent == null) continue;
                RectTransform cell = buttons[i].transform.parent as RectTransform;
                int row = i / 2;
                bool lastCentered = i == buttons.Length - 1 && buttons.Length % 2 == 1;
                int column = i % 2;
                cell.anchoredPosition = new Vector2(
                    lastCentered ? 0f : -100f + column * 200f,
                    165f - row * 133f
                );
                cell.sizeDelta = new Vector2(176f, 126f);
                RectTransform icon = buttons[i].targetGraphic?.rectTransform;
                if (icon != null)
                {
                    icon.anchoredPosition = new Vector2(0f, 10f);
                    icon.sizeDelta = new Vector2(82f, 82f);
                }
                RectTransform label = cell.Find("Label") as RectTransform;
                if (label != null)
                {
                    label.anchoredPosition = new Vector2(0f, -45f);
                    label.sizeDelta = new Vector2(174f, 26f);
                    Text text = label.GetComponent<Text>();
                    if (text != null) text.fontSize = 15;
                }
            }

            Text count = FindChildRecursive(home, "App Count")?.GetComponent<Text>();
            if (count != null) count.text = buttons.Length + " APPS";
        }

        private GameObject BuildVideoApplication(Transform parent)
        {
            Image root = CreateImage(
                VIDEO_APP_NAME,
                parent,
                new Vector2(0f, -66f),
                new Vector2(425f, 714f),
                new Color(0.008f, 0.012f, 0.018f, 1f),
                this.m_RoundedSprite
            );
            Mask mask = root.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            Outline outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.88f, 0.11f, 0.14f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);

            this.m_LiveFeed = CreateRawImage(
                "Live Video Feed",
                root.transform,
                Vector2.zero,
                new Vector2(425f, 714f)
            );
            this.m_LiveFeed.raycastTarget = false;

            Image topShade = CreateImage(
                "Top Shade",
                root.transform,
                new Vector2(0f, 322f),
                new Vector2(425f, 70f),
                new Color(0f, 0f, 0f, 0.62f),
                null
            );
            topShade.raycastTarget = false;
            this.m_LiveStatus = CreateText(
                "Video Status",
                root.transform,
                new Vector2(-151f, 322f),
                new Vector2(104f, 28f),
                14,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Color(1f, 0.24f, 0.26f, 1f)
            );
            this.m_RecordTimer = CreateText(
                "Video Timer",
                root.transform,
                new Vector2(0f, 322f),
                new Vector2(100f, 28f),
                16,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                Color.white
            );
            Button clips = CreateTextButton(
                "Clips",
                root.transform,
                new Vector2(158f, 322f),
                new Vector2(82f, 38f),
                "CLIPS",
                12,
                new Color(0.12f, 0.15f, 0.17f, 0.94f),
                Color.white
            );
            clips.onClick.AddListener(this.OpenLibrary);

            Image bottomShade = CreateImage(
                "Bottom Shade",
                root.transform,
                new Vector2(0f, -296f),
                new Vector2(425f, 122f),
                new Color(0f, 0f, 0f, 0.66f),
                null
            );
            bottomShade.raycastTarget = false;
            Image recordOuter = CreateImage(
                "Record",
                root.transform,
                new Vector2(0f, -296f),
                new Vector2(76f, 76f),
                new Color(0.94f, 0.96f, 0.96f, 1f),
                this.m_CircleSprite
            );
            recordOuter.raycastTarget = true;
            this.m_RecordButton = recordOuter.gameObject.AddComponent<Button>();
            this.m_RecordButton.targetGraphic = recordOuter;
            this.m_RecordButton.onClick.AddListener(this.ToggleRecording);
            this.m_RecordGlyph = CreateImage(
                "Record Glyph",
                recordOuter.transform,
                Vector2.zero,
                new Vector2(56f, 56f),
                new Color(0.96f, 0.12f, 0.15f, 1f),
                this.m_CircleSprite
            );
            this.m_RecordGlyph.raycastTarget = false;
            this.m_RecordHint = CreateText(
                "Record Hint",
                root.transform,
                new Vector2(0f, -247f),
                new Vector2(210f, 24f),
                11,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                new Color(0.82f, 0.86f, 0.87f, 1f)
            );

            this.m_CameraModeButton = CreateTextButton(
                "Camera Mode",
                root.transform,
                new Vector2(-158f, -296f),
                new Vector2(58f, 58f),
                "↻",
                30,
                new Color(0.07f, 0.13f, 0.16f, 0.94f),
                Color.white
            );
            this.m_CameraModeLabel =
                this.m_CameraModeButton.GetComponentInChildren<Text>();
            this.m_CameraModeButton.onClick.AddListener(
                this.ToggleCameraMode
            );

            this.BuildLibrary(root.transform);
            this.BuildViewer(root.transform);
            this.RefreshCameraModeUi();
            this.SetLiveState("● SELFIE", "TAP TO RECORD");
            this.RefreshTimer(0f);
            return root.gameObject;
        }

        private void BuildLibrary(Transform parent)
        {
            Image library = CreateImage(
                "Video Library",
                parent,
                Vector2.zero,
                new Vector2(425f, 714f),
                new Color(0.008f, 0.014f, 0.021f, 1f),
                this.m_RoundedSprite
            );
            this.m_Library = library.gameObject;
            CreateTextButton(
                "Library Back",
                library.transform,
                new Vector2(-165f, 322f),
                new Vector2(72f, 40f),
                "BACK",
                12,
                new Color(0.13f, 0.17f, 0.19f, 1f),
                Color.white
            ).onClick.AddListener(this.CloseLibrary);
            Text title = CreateText(
                "Library Title",
                library.transform,
                new Vector2(0f, 322f),
                new Vector2(190f, 34f),
                18,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                Color.white
            );
            title.text = "VIDEO CLIPS";

            Image viewport = CreateImage(
                "Library Viewport",
                library.transform,
                new Vector2(0f, -24f),
                new Vector2(409f, 632f),
                new Color(0f, 0f, 0f, 0f),
                null
            );
            Mask viewportMask = viewport.gameObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            viewport.raycastTarget = true;
            this.m_LibraryContent = CreateRect(
                "Library Content",
                viewport.transform,
                Vector2.zero,
                new Vector2(0f, 632f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f)
            );
            ScrollRect scroll = library.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = this.m_LibraryContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.scrollSensitivity = 36f;

            this.m_EmptyLibraryText = CreateText(
                "Empty Library",
                viewport.transform,
                Vector2.zero,
                new Vector2(300f, 80f),
                16,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                new Color(0.48f, 0.62f, 0.65f, 1f)
            );
            this.m_EmptyLibraryText.text = "NO VIDEOS YET\nRECORD YOUR FIRST CLIP";
            this.m_Library.SetActive(false);
        }

        private void BuildViewer(Transform parent)
        {
            Image viewer = CreateImage(
                "Video Viewer",
                parent,
                Vector2.zero,
                new Vector2(425f, 714f),
                Color.black,
                this.m_RoundedSprite
            );
            this.m_Viewer = viewer.gameObject;
            this.m_ViewerImage = CreateRawImage(
                "Playback",
                viewer.transform,
                new Vector2(0f, 26f),
                new Vector2(425f, 642f)
            );
            this.m_ViewerImage.raycastTarget = false;

            Image controlBar = CreateImage(
                "Viewer Controls",
                viewer.transform,
                new Vector2(0f, -322f),
                new Vector2(425f, 70f),
                new Color(0.02f, 0.025f, 0.03f, 0.96f),
                null
            );
            controlBar.raycastTarget = false;
            Button back = CreateTextButton(
                "Viewer Back",
                viewer.transform,
                new Vector2(-158f, -322f),
                new Vector2(88f, 42f),
                "BACK",
                12,
                new Color(0.12f, 0.15f, 0.17f, 1f),
                Color.white
            );
            back.onClick.AddListener(this.CloseViewer);
            Button play = CreateTextButton(
                "Viewer Play",
                viewer.transform,
                new Vector2(0f, -322f),
                new Vector2(92f, 42f),
                "PAUSE",
                12,
                new Color(0.09f, 0.38f, 0.4f, 1f),
                Color.white
            );
            this.m_PlayLabel = play.GetComponentInChildren<Text>();
            play.onClick.AddListener(this.TogglePlayback);
            Button delete = CreateTextButton(
                "Viewer Delete",
                viewer.transform,
                new Vector2(151f, -322f),
                new Vector2(100f, 42f),
                "DELETE",
                12,
                new Color(0.5f, 0.06f, 0.07f, 1f),
                Color.white
            );
            this.m_DeleteLabel = delete.GetComponentInChildren<Text>();
            delete.onClick.AddListener(this.DeleteSelectedClip);
            this.m_ViewerInfo = CreateText(
                "Viewer Time",
                viewer.transform,
                new Vector2(0f, 332f),
                new Vector2(180f, 28f),
                14,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                Color.white
            );
            this.m_Viewer.SetActive(false);
        }

        private void DestroyThumbnailTextures()
        {
            foreach (Texture2D texture in this.m_ThumbnailTextures)
                if (texture != null) Destroy(texture);
            this.m_ThumbnailTextures.Clear();
        }

        private void DestroyPhotosVideoSlots()
        {
            foreach (GameObject slot in this.m_PhotosVideoSlots)
                if (slot != null) Destroy(slot);
            this.m_PhotosVideoSlots.Clear();
            foreach (Texture2D texture in this.m_PhotosVideoTextures)
                if (texture != null) Destroy(texture);
            this.m_PhotosVideoTextures.Clear();
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not delete phone video file: " + exception.Message);
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static T[] Append<T>(T[] source, T item)
        {
            source ??= Array.Empty<T>();
            T[] result = new T[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[source.Length] = item;
            return result;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; ++i)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
                Transform nested = FindChildRecursive(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? pivot = null)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
            rect.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            Sprite sprite,
            Vector2? anchor = null,
            Vector2? pivot = null)
        {
            GameObject gameObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage CreateRawImage(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            GameObject gameObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage)
            );
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            RawImage rawImage = gameObject.GetComponent<RawImage>();
            rawImage.color = Color.white;
            return rawImage;
        }

        private Text CreateText(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            FontStyle fontStyle,
            Color color)
        {
            GameObject gameObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = gameObject.GetComponent<Text>();
            text.font = this.m_Font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = fontStyle;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateTextButton(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            string label,
            int fontSize,
            Color background,
            Color foreground)
        {
            Image image = CreateImage(
                name,
                parent,
                position,
                size,
                background,
                this.m_RoundedSprite
            );
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = this.CreateText(
                "Label",
                image.transform,
                Vector2.zero,
                size,
                fontSize,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                foreground
            );
            text.text = label;
            return button;
        }
    }
}
