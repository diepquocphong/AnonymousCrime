using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FranklinGame.Animations;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace FranklinGame.PhoneSystem
{
    /// <summary>
    /// Drives the GC2 Main Camera through a runtime Third Person Shot Camera,
    /// mirrors its final output into the Camera app, captures PNG files and
    /// populates the Photos app.
    /// </summary>
    [DefaultExecutionOrder(990)]
    [DisallowMultipleComponent]
    public sealed class FranklinPhoneSelfieCamera : MonoBehaviour
    {
        private const string PHOTO_FOLDER = "FranklinPhonePhotos";
        private const string VIEWFINDER_NAME = "Viewfinder";
        private const string SHUTTER_NAME = "Shutter";
        private const string STATUS_NAME = "Recording";
        private const string PHOTOS_APP_NAME = "Photos App";
        private const int PHOTO_SLOT_COUNT = 9;
        private const int PHOTO_COLUMNS = 3;
        private const float PHOTO_VIEWPORT_HEIGHT = 696f;
        private const float PHOTO_SLOT_WIDTH = 118f;
        private const float PHOTO_SLOT_HEIGHT = 193f;
        private const float PHOTO_COLUMN_STEP = 137f;
        private const float PHOTO_ROW_STEP = 218f;
        private const float PHOTO_TOP_SLOT_CENTER = 130f;

        private static readonly FieldInfo THIRD_PERSON_SHOULDER_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Shoulder",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_LIFT_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Lift",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_RADIUS_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Radius",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_PIVOT_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_Pivot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_SMOOTH_TIME_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_SmoothTime",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo THIRD_PERSON_MAX_YAW_FIELD =
            typeof(ShotSystemThirdPerson).GetField(
                "m_MaxYaw",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo SHOT_VIEWPORT_FIELD_OF_VIEW_FIELD =
            typeof(ShotSystemViewport).GetField(
                "m_FieldOfView",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        private static readonly FieldInfo SHOT_CAMERA_CLIPPING_FIELD =
            typeof(ShotCamera).GetField(
                "m_Clipping",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        [Header("Selfie Capture")]
        [Tooltip("GC2 Third Person Camera Shot prefab used by the Camera app.")]
        [SerializeField] private ShotCamera m_SelfieShotPrefab;
        [SerializeField, Range(256, 1440)] private int m_CaptureWidth = 725;
        [SerializeField, Range(256, 1440)] private int m_CaptureHeight = 1218;
        [SerializeField, Range(1, 100)] private int m_MaxStoredPhotos = 30;
        [SerializeField] private FilterMode m_FilterMode = FilterMode.Bilinear;

        [Header("Physical Phone Camera Transform")]
        [InspectorName("Phone Camera Position")]
        [Tooltip("World-unit offset from PhoneInstance using its local right/up/forward axes.")]
        [SerializeField] private Vector3 m_PhoneCameraPosition = Vector3.zero;
        [InspectorName("Phone Camera Rotation")]
        [Tooltip("Euler offset applied after the camera aims at the Player.")]
        [SerializeField] private Vector3 m_CameraRotation = Vector3.zero;

        [Header("Back Camera Root Transform")]
        [InspectorName("Back Camera Position")]
        [Tooltip(
            "Offset from the Player root in Character right/up/forward axes. " +
            "BACK never reads or follows a hand bone."
        )]
        [SerializeField] private Vector3 m_BackCameraPosition =
            new(0f, 0.5f, 0f);
        [InspectorName("Back Camera Rotation")]
        [Tooltip("Euler adjustment from the Player root rotation.")]
        [SerializeField] private Vector3 m_BackCameraRotation = Vector3.zero;
        [InspectorName("Back Camera Field Of View")]
        [SerializeField, Range(35f, 120f)]
        private float m_BackCameraFieldOfView = 60f;
        [InspectorName("Back Camera Orbit Smooth Time")]
        [SerializeField, Range(0f, 0.25f)]
        private float m_BackCameraOrbitSmoothTime = 0.06f;

        [Header("GC2 Camera Shot - Third Person")]
        [InspectorName("Shoulder")]
        [Tooltip("GC2 Third Person horizontal offset from the Player pivot.")]
        [SerializeField] private float m_CameraShotShoulder = 0.86f;
        [InspectorName("Lift")]
        [Tooltip("GC2 Third Person vertical offset from the Player pivot.")]
        [SerializeField] private float m_CameraShotLift = 0.5f;
        [InspectorName("Radius")]
        [Tooltip("GC2 Third Person orbit distance from the Player pivot.")]
        [SerializeField, Min(0.01f)] private float m_CameraShotRadius = 3f;

        [Header("Cross-Device Camera Shot Framing")]
        [Tooltip(
            "Keeps the Camera Shot horizontal composition identical across " +
            "phones and tablets with different screen aspect ratios."
        )]
        [SerializeField] private bool m_LockHorizontalFramingAcrossDevices = true;
        [Tooltip(
            "Screen size used while composing Shoulder/Lift/Radius. Use the " +
            "same aspect in Game View when tuning the Camera Shot."
        )]
        [SerializeField] private Vector2 m_ReferenceScreenSize =
            new(1920f, 1080f);
        [Tooltip(
            "Vertical Camera Shot FOV at the reference screen size."
        )]
        [SerializeField, Range(1f, 179f)]
        private float m_ReferenceVerticalFieldOfView = 60f;

        [Header("Selfie Camera Framing")]
        [Tooltip("Aim offset from the Player Head using Character right/up/forward axes.")]
        [SerializeField] private Vector3 m_PlayerAimOffset =
            new(0f, -0.07f, 0f);
        [Tooltip(
            "Preserves the Camera Shot horizontal framing in the portrait " +
            "RenderTexture. Player keeps the same left/right screen position."
        )]
        [SerializeField] private bool m_MatchCameraShotHorizontalFraming = true;
        [Tooltip(
            "Manual RenderTexture vertical FOV. Only used when Match Camera " +
            "Shot Horizontal Framing is disabled."
        )]
        [SerializeField, Range(35f, 170f)] private float m_SelfieFieldOfView = 70f;
        [SerializeField, Range(0.01f, 0.15f)] private float m_SelfieNearClip = 0.04f;

        [Header("Player Facing Camera Shot")]
        [FormerlySerializedAs("m_FaceMainCamera")]
        [Tooltip("Turns the Player body and head toward the active GC2 selfie Camera Shot.")]
        [SerializeField] private bool m_FaceCameraShot = true;
        [Tooltip(
            "Multiplier applied to GC2 Character Motion Angular Speed only " +
            "while selfie mode is active."
        )]
        [SerializeField, Min(1f)] private float m_SelfieTurnSpeedMultiplier = 3f;

        private FranklinPhoneSystem m_PhoneSystem;
        private FranklinPhoneHandPresentation m_HandPresentation;
        private MainCamera m_Gc2MainCamera;
        private Camera m_MainCamera;
        private Camera m_SelfieCamera;
        private ShotCamera m_RuntimeSelfieShot;
        private ShotCamera m_RuntimeBackCameraShot;
        private ShotTypeThirdPerson m_RuntimeBackCameraShotType;
        private ShotSystemThirdPerson m_RuntimeBackThirdPerson;
        private ShotSystemViewport m_RuntimeBackShotViewport;
        private EnablerFloat m_RuntimeBackShotFieldOfView;
        private GameObject m_BackCameraPivot;
        private int m_LastBackCameraPivotFrame = -1;
        private Character m_BackCameraHiddenPlayer;
        private FranklinObjectDirectionToggle m_BackCameraObjectDirectionToggle;
        private bool m_OwnsBackCameraObjectDirection;
        private readonly Dictionary<Renderer, bool> m_BackCameraRendererStates =
            new();
        private ShotTypeThirdPerson m_RuntimeSelfieShotType;
        private ShotSystemThirdPerson m_RuntimeThirdPerson;
        private ShotSystemViewport m_RuntimeShotViewport;
        private EnablerFloat m_RuntimeShotFieldOfView;
        private MainCamera m_MainCameraShotOwner;
        private ShotCamera m_PreSelfieShot;
        private float m_PreSelfieFieldOfView;
        private bool m_HasPreSelfieFieldOfView;
        private bool m_IsMainCameraShotActive;
        private Quaternion m_BackCameraEntryRotation = Quaternion.identity;
        private bool m_HasBackCameraEntryRotation;
        private float m_LastAppliedShotShoulder;
        private float m_LastAppliedShotLift;
        private float m_LastAppliedShotRadius;
        private float m_LastAppliedDeviceFieldOfView = -1f;
        private bool m_HasAppliedShotFraming;
        private float m_LastAppliedBackFieldOfView = -1f;
        private RenderTexture m_RenderTexture;
        private RawImage m_Viewfinder;
        private Image m_FlashImage;
        private Button m_ShutterButton;
        private Text m_StatusText;
        private readonly List<RawImage> m_PhotoSlots = new();
        private readonly List<Button> m_PhotoButtons = new();
        private readonly List<Texture2D> m_GalleryTextures = new();
        private readonly List<string> m_GalleryPaths = new();
        private ScrollRect m_PhotoScrollRect;
        private RectTransform m_PhotoScrollContent;
        private GameObject m_PhotoViewer;
        private RawImage m_PhotoViewerImage;
        private Text m_PhotoViewerInfo;
        private Button m_PhotoViewerBackButton;
        private Button m_PhotoViewerDeleteButton;
        private Text m_PhotoViewerDeleteLabel;
        private Text m_PhotoEmptyText;
        private int m_SelectedPhotoIndex = -1;
        private bool m_DeletePhotoArmed;
        private Character m_Player;
        private Transform m_PlayerHead;
        private Transform m_PlayerChest;
        private int m_FacingLayerKey = -1;
        private Character m_TurnSpeedOwner;
        private float m_BasePlayerAngularSpeed;
        private float m_LastAppliedPlayerAngularSpeed;
        private bool m_HasPlayerAngularSpeedOverride;
        private Coroutine m_CaptureRoutine;
        private bool m_Initialized;
        private bool m_IsActive;
        private bool m_IsBackCameraMode;

        public bool IsActive => this.m_IsActive;
        public bool IsBackCameraMode => this.m_IsBackCameraMode;
        public bool IsConfigured => this.m_SelfieShotPrefab != null;
        public RenderTexture LiveTexture => this.m_RenderTexture;
        public int StoredPhotoCount { get; private set; }
        public string PhotoDirectory => Path.Combine(
            Application.persistentDataPath,
            PHOTO_FOLDER
        );

        public event Action<Texture2D, string> EventPhotoCaptured;

        public void Configure(ShotCamera selfieShotPrefab)
        {
            if (selfieShotPrefab != null)
                this.m_SelfieShotPrefab = selfieShotPrefab;
        }

        public void Initialize(
            FranklinPhoneSystem phoneSystem,
            FranklinPhoneHandPresentation handPresentation)
        {
            this.m_PhoneSystem = phoneSystem;
            this.m_HandPresentation = handPresentation;
            if (this.m_Initialized) return;

            this.ResolveInterface();
            this.EnsureRenderResources();
            this.ReloadGalleryFromDisk();
            this.m_Initialized = true;
        }

        public void SetSelfieActive(bool active)
        {
            if (!this.m_Initialized)
            {
                this.Initialize(
                    this.GetComponent<FranklinPhoneSystem>(),
                    this.GetComponent<FranklinPhoneHandPresentation>()
                );
            }

            this.m_IsActive = active;
            if (active)
            {
                this.EnsureRenderResources();
                this.ResolveMainCamera();
                this.ResolvePlayer();
                if (this.m_IsBackCameraMode)
                    this.CaptureBackCameraEntryRotation();
                this.m_HandPresentation?.SyncPhoneTransform();
                this.EnsureMainCameraShotActive();
                this.UpdatePlayerPresentationForCameraMode();
                this.SyncSelfieCamera();
                if (this.m_SelfieCamera != null)
                    this.m_SelfieCamera.enabled = true;
                if (this.m_Viewfinder != null)
                    this.m_Viewfinder.texture = this.m_RenderTexture;
                if (this.m_StatusText != null)
                    this.m_StatusText.text = this.m_IsBackCameraMode
                        ? "● BACK"
                        : "● SELFIE";
                this.m_HandPresentation?.SetSelfieMode(
                    true,
                    this.m_IsBackCameraMode
                        ? null
                        : this.GetCameraShotLookTarget()
                );
            }
            else
            {
                if (this.m_CaptureRoutine != null)
                {
                    this.StopCoroutine(this.m_CaptureRoutine);
                    this.m_CaptureRoutine = null;
                }
                if (this.m_SelfieCamera != null)
                    this.m_SelfieCamera.enabled = false;
                if (this.m_ShutterButton != null)
                    this.m_ShutterButton.interactable = true;
                this.RestoreMainCameraShot();
                this.ReleasePlayerFacing();
                this.RestorePlayerTurnSpeed();
                this.RestoreBackCameraPlayerPresentation();
                this.m_PhoneSystem?.SetBackCameraFastMovementEnabled(false);
                this.m_HandPresentation?.SetPhoneModelHidden(false);
                this.m_HasBackCameraEntryRotation = false;
                this.m_HandPresentation?.SetSelfieMode(false);
            }
        }

        public void SetBackCameraMode(bool backCamera)
        {
            if (this.m_IsBackCameraMode == backCamera) return;

            // Capture the outgoing SELFIE/gameplay view before changing mode.
            // BACK starts at a true world-space 180-degree yaw from that view.
            if (backCamera && this.m_IsActive)
            {
                this.ResolveMainCamera();
                this.ResolvePlayer();
                this.CaptureBackCameraEntryRotation();
            }
            else if (!backCamera)
            {
                this.m_HasBackCameraEntryRotation = false;
            }
            this.m_IsBackCameraMode = backCamera;
            if (!this.m_IsActive) return;

            this.m_HandPresentation?.SyncPhoneTransform();
            this.ResolveMainCamera();
            this.ResolvePlayer();
            this.UpdatePlayerPresentationForCameraMode();
            this.EnsureMainCameraShotActive();
            this.SyncSelfieCamera();
            if (this.m_StatusText != null)
                this.m_StatusText.text = backCamera ? "● BACK" : "● SELFIE";
            this.m_HandPresentation?.SetSelfieMode(
                true,
                backCamera ? null : this.GetCameraShotLookTarget()
            );
        }

        public void CapturePhoto()
        {
            if (!this.m_IsActive || this.m_CaptureRoutine != null ||
                this.m_RenderTexture == null)
            {
                return;
            }

            this.m_CaptureRoutine = this.StartCoroutine(this.CapturePhotoRoutine());
        }

        public void RefreshGallery()
        {
            this.ClosePhotoViewer();
            this.ReloadGalleryFromDisk();
        }

        public void ClosePhotoViewer()
        {
            this.m_SelectedPhotoIndex = -1;
            this.m_DeletePhotoArmed = false;
            if (this.m_PhotoViewerImage != null)
                this.m_PhotoViewerImage.texture = null;
            if (this.m_PhotoViewerDeleteLabel != null)
                this.m_PhotoViewerDeleteLabel.text = "DELETE";
            if (this.m_PhotoViewer != null)
                this.m_PhotoViewer.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!this.m_IsActive) return;

            this.ResolveMainCamera();
            this.ResolvePlayer();
            this.EnsureMainCameraShotActive();
            this.UpdatePlayerPresentationForCameraMode();
            this.SyncSelfieCamera();

            this.m_HandPresentation?.SetSelfieMode(
                true,
                this.m_IsBackCameraMode
                    ? null
                    : this.GetCameraShotLookTarget()
            );
        }

        private void OnDisable()
        {
            if (this.m_Initialized) this.SetSelfieActive(false);
        }

        private void OnDestroy()
        {
            this.RestoreMainCameraShot();
            this.BindGc2MainCamera(null);
            this.ReleasePlayerFacing();
            this.RestorePlayerTurnSpeed();
            this.RestoreBackCameraPlayerPresentation();
            this.m_PhoneSystem?.SetBackCameraFastMovementEnabled(false);
            this.m_HandPresentation?.SetPhoneModelHidden(false);
            if (this.m_Player != null)
                this.m_Player.EventBeforeUpdate -= this.OnPlayerBeforeUpdate;
            this.m_HandPresentation?.SetSelfieMode(false);

            if (this.m_ShutterButton != null)
                this.m_ShutterButton.onClick.RemoveListener(this.CapturePhoto);
            if (this.m_PhotoViewerBackButton != null)
                this.m_PhotoViewerBackButton.onClick.RemoveListener(
                    this.ClosePhotoViewer
                );
            if (this.m_PhotoViewerDeleteButton != null)
                this.m_PhotoViewerDeleteButton.onClick.RemoveListener(
                    this.DeleteSelectedPhoto
                );

            if (this.m_SelfieCamera != null)
            {
                this.m_SelfieCamera.targetTexture = null;
                Destroy(this.m_SelfieCamera.gameObject);
            }

            if (this.m_RuntimeSelfieShot != null)
                Destroy(this.m_RuntimeSelfieShot.gameObject);

            if (this.m_RuntimeBackCameraShot != null)
                Destroy(this.m_RuntimeBackCameraShot.gameObject);

            if (this.m_BackCameraPivot != null)
                Destroy(this.m_BackCameraPivot);

            if (this.m_RenderTexture != null)
            {
                this.m_RenderTexture.Release();
                Destroy(this.m_RenderTexture);
            }

            this.DestroyGalleryTextures();
        }

        private void ResolveInterface()
        {
            Transform viewfinder = FindChildRecursive(
                this.transform,
                VIEWFINDER_NAME
            );
            if (viewfinder != null)
            {
                this.ConfigureFullPhoneViewfinder(
                    viewfinder.GetComponent<RectTransform>()
                );

                Transform feed = viewfinder.Find("Live Selfie Feed");
                if (feed == null)
                {
                    GameObject feedObject = new(
                        "Live Selfie Feed",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(RawImage)
                    );
                    RectTransform feedRect =
                        feedObject.GetComponent<RectTransform>();
                    feedRect.SetParent(viewfinder, false);
                    Stretch(feedRect);
                    feedRect.SetAsFirstSibling();
                    feed = feedRect;
                }

                this.m_Viewfinder = feed.GetComponent<RawImage>();
                this.m_Viewfinder.color = Color.white;
                this.m_Viewfinder.raycastTarget = false;

                Transform flash = viewfinder.Find("Selfie Flash");
                if (flash == null)
                {
                    GameObject flashObject = new(
                        "Selfie Flash",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image)
                    );
                    RectTransform flashRect =
                        flashObject.GetComponent<RectTransform>();
                    flashRect.SetParent(viewfinder, false);
                    Stretch(flashRect);
                    flash = flashRect;
                }
                this.m_FlashImage = flash.GetComponent<Image>();
                this.m_FlashImage.color = new Color(1f, 1f, 1f, 0f);
                this.m_FlashImage.raycastTarget = false;
            }

            Transform shutter = FindChildRecursive(this.transform, SHUTTER_NAME);
            if (shutter != null)
            {
                RectTransform shutterRect = shutter as RectTransform;
                if (shutterRect != null)
                    shutterRect.anchoredPosition = new Vector2(0f, -295f);

                this.m_ShutterButton = shutter.GetComponent<Button>();
                if (this.m_ShutterButton == null)
                    this.m_ShutterButton = shutter.gameObject.AddComponent<Button>();
                Graphic shutterGraphic = shutter.GetComponent<Graphic>();
                if (shutterGraphic != null) shutterGraphic.raycastTarget = true;
                this.m_ShutterButton.targetGraphic = shutterGraphic;
                this.m_ShutterButton.onClick.RemoveListener(this.CapturePhoto);
                this.m_ShutterButton.onClick.AddListener(this.CapturePhoto);
            }

            Transform status = FindChildRecursive(this.transform, STATUS_NAME);
            this.m_StatusText = status != null ? status.GetComponent<Text>() : null;

            RectTransform photosApp = FindChildRecursive(
                this.transform,
                PHOTOS_APP_NAME
            ) as RectTransform;
            this.ConfigurePhotosInterface(photosApp);

            for (int i = 0; i < PHOTO_SLOT_COUNT; ++i)
            {
                Transform slot = FindChildRecursive(
                    this.transform,
                    $"Photo {i + 1}"
                );
                if (slot == null) continue;

                this.RegisterPhotoSlot(slot as RectTransform, i);
            }

            this.EnsurePhotoSlotCapacity(PHOTO_SLOT_COUNT);
            this.UpdatePhotoScrollLayout(0, true);
            this.EnsurePhotoViewer(photosApp);
        }

        private void ConfigurePhotosInterface(RectTransform photosApp)
        {
            if (photosApp == null) return;

            Vector2 center = new(0.5f, 0.5f);
            photosApp.anchorMin = center;
            photosApp.anchorMax = center;
            photosApp.pivot = center;
            photosApp.anchoredPosition = new Vector2(0f, -57f);
            photosApp.sizeDelta = new Vector2(425f, 696f);

            Image background = photosApp.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.012f, 0.018f, 0.026f, 1f);
                background.raycastTarget = false;
            }

            Mask mask = photosApp.GetComponent<Mask>();
            if (mask == null) mask = photosApp.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            Outline outline = photosApp.GetComponent<Outline>();
            if (outline == null)
                outline = photosApp.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.16f, 0.68f, 0.67f, 0.62f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            Transform sectionTitle = photosApp.Find("Section Title");
            Transform sectionRule = photosApp.Find("Section Rule");
            if (sectionTitle != null) sectionTitle.gameObject.SetActive(false);
            if (sectionRule != null) sectionRule.gameObject.SetActive(false);

            this.EnsurePhotoScrollView(photosApp);

            Transform empty = photosApp.Find("Photo Empty State");
            if (empty == null)
            {
                this.m_PhotoEmptyText = CreateRuntimeText(
                    "Photo Empty State",
                    photosApp,
                    "NO PHOTOS YET",
                    18,
                    TextAnchor.MiddleCenter,
                    new Color(0.65f, 0.71f, 0.71f, 1f)
                );
                RectTransform emptyRect = this.m_PhotoEmptyText.rectTransform;
                emptyRect.anchorMin = center;
                emptyRect.anchorMax = center;
                emptyRect.pivot = center;
                emptyRect.anchoredPosition = Vector2.zero;
                emptyRect.sizeDelta = new Vector2(320f, 60f);
            }
            else
            {
                this.m_PhotoEmptyText = empty.GetComponent<Text>();
            }
            if (this.m_PhotoEmptyText != null)
                this.m_PhotoEmptyText.transform.SetAsLastSibling();
        }

        private void EnsurePhotoScrollView(RectTransform photosApp)
        {
            if (photosApp == null) return;

            Transform areaTransform = photosApp.Find("Photos Scroll Area");
            RectTransform areaRect;
            if (areaTransform == null)
            {
                GameObject areaObject = new(
                    "Photos Scroll Area",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(ScrollRect)
                );
                areaRect = areaObject.GetComponent<RectTransform>();
                areaRect.SetParent(photosApp, false);
                Stretch(areaRect);
                Image areaImage = areaObject.GetComponent<Image>();
                areaImage.color = Color.clear;
                areaImage.raycastTarget = true;
                areaTransform = areaRect;
            }
            else
            {
                areaRect = areaTransform as RectTransform;
            }
            if (areaRect == null) return;

            this.m_PhotoScrollRect =
                areaRect.GetComponent<ScrollRect>() ??
                areaRect.gameObject.AddComponent<ScrollRect>();

            Transform viewportTransform = areaRect.Find("Viewport");
            RectTransform viewportRect;
            if (viewportTransform == null)
            {
                GameObject viewportObject = new(
                    "Viewport",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(RectMask2D)
                );
                viewportRect = viewportObject.GetComponent<RectTransform>();
                viewportRect.SetParent(areaRect, false);
                Stretch(viewportRect);
                Image viewportImage = viewportObject.GetComponent<Image>();
                viewportImage.color = Color.clear;
                viewportImage.raycastTarget = false;
                viewportTransform = viewportRect;
            }
            else
            {
                viewportRect = viewportTransform as RectTransform;
                if (viewportTransform.GetComponent<RectMask2D>() == null)
                    viewportTransform.gameObject.AddComponent<RectMask2D>();
            }
            if (viewportRect == null) return;

            Transform contentTransform = viewportRect.Find("Content");
            if (contentTransform == null)
            {
                GameObject contentObject = new(
                    "Content",
                    typeof(RectTransform)
                );
                this.m_PhotoScrollContent =
                    contentObject.GetComponent<RectTransform>();
                this.m_PhotoScrollContent.SetParent(viewportRect, false);
            }
            else
            {
                this.m_PhotoScrollContent =
                    contentTransform as RectTransform;
            }
            if (this.m_PhotoScrollContent == null) return;

            this.m_PhotoScrollContent.anchorMin = new Vector2(0f, 1f);
            this.m_PhotoScrollContent.anchorMax = new Vector2(1f, 1f);
            this.m_PhotoScrollContent.pivot = new Vector2(0.5f, 1f);
            this.m_PhotoScrollContent.anchoredPosition = Vector2.zero;
            this.m_PhotoScrollContent.sizeDelta =
                new Vector2(0f, PHOTO_VIEWPORT_HEIGHT);

            this.m_PhotoScrollRect.viewport = viewportRect;
            this.m_PhotoScrollRect.content = this.m_PhotoScrollContent;
            this.m_PhotoScrollRect.horizontal = false;
            this.m_PhotoScrollRect.vertical = true;
            this.m_PhotoScrollRect.movementType =
                ScrollRect.MovementType.Elastic;
            this.m_PhotoScrollRect.elasticity = 0.08f;
            this.m_PhotoScrollRect.inertia = true;
            this.m_PhotoScrollRect.decelerationRate = 0.135f;
            this.m_PhotoScrollRect.scrollSensitivity = 55f;
            this.m_PhotoScrollRect.horizontalScrollbar = null;
            this.m_PhotoScrollRect.verticalScrollbar = null;

            areaRect.SetAsFirstSibling();
        }

        private void RegisterPhotoSlot(RectTransform slot, int index)
        {
            if (slot == null || index < 0) return;

            this.ConfigurePhotoSlot(slot, index);
            Transform image = slot.Find("Captured Image");
            if (image == null)
            {
                GameObject imageObject = new(
                    "Captured Image",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage)
                );
                RectTransform imageRect =
                    imageObject.GetComponent<RectTransform>();
                imageRect.SetParent(slot, false);
                Stretch(imageRect);
                imageRect.SetAsFirstSibling();
                image = imageRect;
            }

            RawImage rawImage = image.GetComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
            rawImage.texture = null;
            rawImage.gameObject.SetActive(false);
            SetListItem(this.m_PhotoSlots, index, rawImage);
            slot.gameObject.SetActive(false);
        }

        private void EnsurePhotoSlotCapacity(int count)
        {
            if (this.m_PhotoScrollContent == null) return;

            for (int index = 0; index < count; ++index)
            {
                if (index < this.m_PhotoSlots.Count &&
                    this.m_PhotoSlots[index] != null)
                {
                    continue;
                }

                GameObject slotObject = new(
                    $"Photo {index + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                RectTransform slotRect =
                    slotObject.GetComponent<RectTransform>();
                slotRect.SetParent(this.m_PhotoScrollContent, false);

                Image background = slotObject.GetComponent<Image>();
                Image template = this.m_PhotoSlots.Count > 0 &&
                                 this.m_PhotoSlots[0] != null
                    ? this.m_PhotoSlots[0].transform.parent
                        .GetComponent<Image>()
                    : null;
                background.color =
                    new Color(0.015f, 0.022f, 0.030f, 1f);
                background.raycastTarget = true;
                if (template != null)
                {
                    background.sprite = template.sprite;
                    background.type = template.type;
                }

                this.RegisterPhotoSlot(slotRect, index);
            }
        }

        private void UpdatePhotoScrollLayout(int itemCount, bool resetToTop)
        {
            if (this.m_PhotoScrollContent == null) return;

            int rows = Mathf.Max(
                1,
                Mathf.CeilToInt(itemCount / (float)PHOTO_COLUMNS)
            );
            float contentHeight = Mathf.Max(
                PHOTO_VIEWPORT_HEIGHT,
                PHOTO_TOP_SLOT_CENTER + PHOTO_SLOT_HEIGHT * 0.5f +
                (rows - 1) * PHOTO_ROW_STEP + 33.5f
            );
            this.m_PhotoScrollContent.sizeDelta =
                new Vector2(0f, contentHeight);

            if (this.m_PhotoScrollRect == null) return;
            this.m_PhotoScrollRect.vertical =
                contentHeight > PHOTO_VIEWPORT_HEIGHT + 0.5f;
            if (resetToTop)
            {
                this.m_PhotoScrollContent.anchoredPosition = Vector2.zero;
                this.m_PhotoScrollRect.StopMovement();
                Canvas.ForceUpdateCanvases();
                this.m_PhotoScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void ConfigurePhotoSlot(RectTransform slot, int index)
        {
            if (slot == null || index < 0 ||
                this.m_PhotoScrollContent == null)
            {
                return;
            }

            slot.SetParent(this.m_PhotoScrollContent, false);
            int column = index % PHOTO_COLUMNS;
            int row = index / PHOTO_COLUMNS;
            Vector2 topCenter = new(0.5f, 1f);
            slot.anchorMin = topCenter;
            slot.anchorMax = topCenter;
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2(
                -PHOTO_COLUMN_STEP + column * PHOTO_COLUMN_STEP,
                -PHOTO_TOP_SLOT_CENTER - row * PHOTO_ROW_STEP
            );
            slot.sizeDelta = new Vector2(PHOTO_SLOT_WIDTH, PHOTO_SLOT_HEIGHT);

            Image background = slot.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.015f, 0.022f, 0.030f, 1f);
                background.raycastTarget = true;
            }

            Mask mask = slot.GetComponent<Mask>();
            if (mask == null) mask = slot.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            Outline outline = slot.GetComponent<Outline>();
            if (outline == null) outline = slot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            outline.effectDistance = new Vector2(1f, -1f);

            for (int i = 0; i < slot.childCount; ++i)
            {
                Transform child = slot.GetChild(i);
                if (child.name == "Tag" || child.name == "Map Line")
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }

            Button button = slot.GetComponent<Button>();
            if (button == null) button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            int photoIndex = index;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => this.ShowPhoto(photoIndex));
            SetListItem(this.m_PhotoButtons, index, button);
        }

        private void EnsurePhotoViewer(RectTransform photosApp)
        {
            if (photosApp == null || this.m_PhotoViewer != null) return;

            GameObject viewer = new(
                "Photo Viewer",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            RectTransform viewerRect = viewer.GetComponent<RectTransform>();
            viewerRect.SetParent(photosApp, false);
            Stretch(viewerRect);
            Image viewerBackground = viewer.GetComponent<Image>();
            viewerBackground.color = new Color(0.006f, 0.009f, 0.013f, 1f);
            viewerBackground.raycastTarget = true;

            GameObject imageObject = new(
                "Viewed Photo",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage)
            );
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(viewerRect, false);
            Stretch(imageRect);
            this.m_PhotoViewerImage = imageObject.GetComponent<RawImage>();
            this.m_PhotoViewerImage.color = Color.white;
            this.m_PhotoViewerImage.raycastTarget = false;

            this.m_PhotoViewerBackButton = CreateRuntimeButton(
                "Photo Viewer Back",
                viewerRect,
                "BACK",
                new Vector2(-159f, 309f),
                new Vector2(78f, 44f),
                new Color(0.02f, 0.07f, 0.09f, 0.88f),
                out _
            );
            this.m_PhotoViewerBackButton.onClick.AddListener(
                this.ClosePhotoViewer
            );

            this.m_PhotoViewerInfo = CreateRuntimeText(
                "Photo Viewer Info",
                viewerRect,
                string.Empty,
                14,
                TextAnchor.MiddleRight,
                new Color(1f, 1f, 1f, 0.86f)
            );
            RectTransform infoRect = this.m_PhotoViewerInfo.rectTransform;
            infoRect.anchorMin = new Vector2(0.5f, 0.5f);
            infoRect.anchorMax = new Vector2(0.5f, 0.5f);
            infoRect.pivot = new Vector2(0.5f, 0.5f);
            infoRect.anchoredPosition = new Vector2(94f, 309f);
            infoRect.sizeDelta = new Vector2(170f, 44f);

            this.m_PhotoViewerDeleteButton = CreateRuntimeButton(
                "Delete Viewed Photo",
                viewerRect,
                "DELETE",
                new Vector2(0f, -305f),
                new Vector2(188f, 52f),
                new Color(0.48f, 0.08f, 0.065f, 0.94f),
                out this.m_PhotoViewerDeleteLabel
            );
            this.m_PhotoViewerDeleteButton.onClick.AddListener(
                this.DeleteSelectedPhoto
            );

            this.m_PhotoViewer = viewer;
            this.m_PhotoViewer.SetActive(false);
        }

        private void ShowPhoto(int index)
        {
            if (index < 0 || index >= this.m_GalleryTextures.Count ||
                index >= this.m_GalleryPaths.Count || this.m_PhotoViewer == null)
            {
                return;
            }

            this.m_SelectedPhotoIndex = index;
            this.m_DeletePhotoArmed = false;
            if (this.m_PhotoViewerDeleteLabel != null)
                this.m_PhotoViewerDeleteLabel.text = "DELETE";
            if (this.m_PhotoViewerImage != null)
                this.m_PhotoViewerImage.texture = this.m_GalleryTextures[index];
            if (this.m_PhotoViewerInfo != null)
            {
                string path = this.m_GalleryPaths[index];
                DateTime timestamp = File.GetLastWriteTime(path);
                this.m_PhotoViewerInfo.text = timestamp.ToString("dd/MM/yyyy  HH:mm");
            }
            this.m_PhotoViewer.SetActive(true);
            this.m_PhotoViewer.transform.SetAsLastSibling();
        }

        private void DeleteSelectedPhoto()
        {
            int index = this.m_SelectedPhotoIndex;
            if (index < 0 || index >= this.m_GalleryPaths.Count) return;

            if (!this.m_DeletePhotoArmed)
            {
                this.m_DeletePhotoArmed = true;
                if (this.m_PhotoViewerDeleteLabel != null)
                    this.m_PhotoViewerDeleteLabel.text = "TAP AGAIN TO DELETE";
                return;
            }

            string path = this.m_GalleryPaths[index];
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not delete selfie '{path}': {exception.Message}",
                    this
                );
                if (this.m_PhotoViewerDeleteLabel != null)
                    this.m_PhotoViewerDeleteLabel.text = "DELETE FAILED";
                return;
            }

            this.ClosePhotoViewer();
            this.ReloadGalleryFromDisk();
            this.m_PhoneSystem?.RefreshPhotosHeader();
        }

        private Text CreateRuntimeText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor anchor,
            Color color)
        {
            GameObject textObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            Text template = FindChildRecursive(this.transform, "Header Title")
                ?.GetComponent<Text>();
            text.font = template != null
                ? template.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateRuntimeButton(
            string name,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            Color color,
            out Text labelText)
        {
            GameObject buttonObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            labelText = this.CreateRuntimeText(
                "Label",
                buttonObject.transform,
                label,
                13,
                TextAnchor.MiddleCenter,
                Color.white
            );
            Stretch(labelText.rectTransform);
            return button;
        }

        private void ConfigureFullPhoneViewfinder(RectTransform viewfinder)
        {
            if (viewfinder == null) return;

            Vector2 center = new(0.5f, 0.5f);
            RectTransform cameraApp = viewfinder.parent as RectTransform;
            if (cameraApp != null)
            {
                cameraApp.anchorMin = center;
                cameraApp.anchorMax = center;
                cameraApp.pivot = center;
                cameraApp.anchoredPosition = new Vector2(0f, -66f);
                cameraApp.sizeDelta = new Vector2(425f, 714f);

                Transform sectionTitle = cameraApp.Find("Section Title");
                Transform sectionRule = cameraApp.Find("Section Rule");
                if (sectionTitle != null) sectionTitle.gameObject.SetActive(false);
                if (sectionRule != null) sectionRule.gameObject.SetActive(false);
            }

            viewfinder.anchorMin = center;
            viewfinder.anchorMax = center;
            viewfinder.pivot = center;
            viewfinder.anchoredPosition = Vector2.zero;
            viewfinder.sizeDelta = new Vector2(425f, 714f);

            Mask mask = viewfinder.GetComponent<Mask>();
            if (mask == null) mask = viewfinder.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            Outline outline = viewfinder.GetComponent<Outline>();
            if (outline == null)
                outline = viewfinder.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.16f, 0.68f, 0.67f, 0.62f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            Transform recording = viewfinder.Find(STATUS_NAME);
            RectTransform recordingRect = recording as RectTransform;
            if (recordingRect != null)
                recordingRect.anchoredPosition = new Vector2(-164f, 319f);

            for (int i = 0; i < viewfinder.childCount; ++i)
            {
                RectTransform line = viewfinder.GetChild(i) as RectTransform;
                if (line == null || line.name != "Map Line") continue;

                bool vertical = Mathf.Abs(
                    Mathf.DeltaAngle(line.localEulerAngles.z, 90f)
                ) < 1f;
                if (vertical)
                {
                    float side = line.anchoredPosition.x < 0f ? -70f : 70f;
                    line.anchoredPosition = new Vector2(side, 0f);
                    line.sizeDelta = new Vector2(702f, 2f);
                }
                else
                {
                    float side = line.anchoredPosition.y < 0f ? -119f : 119f;
                    line.anchoredPosition = new Vector2(0f, side);
                    line.sizeDelta = new Vector2(413f, 2f);
                }
            }
        }

        private void EnsureRenderResources()
        {
            int width = Mathf.Clamp(this.m_CaptureWidth, 256, 1440);
            int height = Mathf.Clamp(this.m_CaptureHeight, 256, 1440);
            if (this.m_RenderTexture == null ||
                this.m_RenderTexture.width != width ||
                this.m_RenderTexture.height != height)
            {
                if (this.m_RenderTexture != null)
                {
                    if (this.m_SelfieCamera != null)
                        this.m_SelfieCamera.targetTexture = null;
                    this.m_RenderTexture.Release();
                    Destroy(this.m_RenderTexture);
                }

                this.m_RenderTexture = new RenderTexture(
                    width,
                    height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default
                )
                {
                    name = "Franklin Selfie Render Texture",
                    filterMode = this.m_FilterMode,
                    wrapMode = TextureWrapMode.Clamp,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                this.m_RenderTexture.Create();
            }

            if (this.m_SelfieCamera == null)
            {
                GameObject cameraObject = new("Franklin Selfie Camera");
                cameraObject.hideFlags = HideFlags.HideInHierarchy;
                this.m_SelfieCamera = cameraObject.AddComponent<Camera>();
                this.m_SelfieCamera.enabled = false;
                DontDestroyOnLoad(cameraObject);
            }

            this.m_SelfieCamera.targetTexture = this.m_RenderTexture;
            if (this.m_Viewfinder != null)
                this.m_Viewfinder.texture = this.m_RenderTexture;
        }

        private void ResolveMainCamera()
        {
            MainCamera gc2MainCamera = ShortcutMainCamera.Get<MainCamera>();
            if (gc2MainCamera != this.m_Gc2MainCamera)
                this.BindGc2MainCamera(gc2MainCamera);

            Camera camera = gc2MainCamera != null
                ? gc2MainCamera.GetComponent<Camera>()
                : ShortcutMainCamera.Get<Camera>();
            if (camera == null) camera = Camera.main;
            if (camera == this.m_SelfieCamera) camera = null;
            this.m_MainCamera = camera;
        }

        private void BindGc2MainCamera(MainCamera mainCamera)
        {
            if (this.m_Gc2MainCamera == mainCamera) return;

            if (this.m_MainCameraShotOwner == this.m_Gc2MainCamera)
                this.RestoreMainCameraShot();
            if (this.m_Gc2MainCamera != null)
            {
                this.m_Gc2MainCamera.EventBeforeUpdate -=
                    this.OnGc2MainCameraBeforeUpdate;
            }

            this.m_Gc2MainCamera = mainCamera;
            if (this.m_Gc2MainCamera != null)
            {
                this.m_Gc2MainCamera.EventBeforeUpdate +=
                    this.OnGc2MainCameraBeforeUpdate;
            }
        }

        private void OnGc2MainCameraBeforeUpdate()
        {
            if (!this.m_IsActive || this.m_Gc2MainCamera == null) return;

            this.m_HandPresentation?.SyncPhoneTransform();
            this.ResolvePlayer();
            this.EnsureMainCameraShotActive();
        }

        private void ResolvePlayer()
        {
            Character character = ShortcutPlayer.Get<Character>();
            if (this.m_Player == character) return;

            this.RestoreBackCameraPlayerPresentation();
            this.ReleasePlayerFacing();
            this.RestorePlayerTurnSpeed();
            if (this.m_Player != null)
                this.m_Player.EventBeforeUpdate -= this.OnPlayerBeforeUpdate;
            this.m_Player = character;
            if (this.m_Player != null)
                this.m_Player.EventBeforeUpdate += this.OnPlayerBeforeUpdate;
            this.ResolvePlayerBones();
            if (this.m_IsActive)
                this.UpdatePlayerPresentationForCameraMode();
        }

        private void OnPlayerBeforeUpdate()
        {
            if (this.m_IsActive) this.ApplyPlayerSelfieTurnSpeed();
        }

        private void ResolvePlayerBones()
        {
            this.m_PlayerHead = null;
            this.m_PlayerChest = null;
            if (this.m_Player == null) return;

            Animator animator = this.m_Player.Animim?.Animator;
            if (animator == null)
                animator = this.m_Player.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman) return;

            this.m_PlayerHead = animator.GetBoneTransform(HumanBodyBones.Head);
            this.m_PlayerChest =
                animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                animator.GetBoneTransform(HumanBodyBones.Chest) ??
                animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        private void UpdatePlayerFacing()
        {
            if (this.m_IsBackCameraMode || !this.m_FaceCameraShot ||
                this.m_Player?.Facing == null)
            {
                return;
            }

            this.ApplyPlayerSelfieTurnSpeed();

            ShotCamera shot = this.m_RuntimeSelfieShot != null
                ? this.m_RuntimeSelfieShot
                : this.m_Gc2MainCamera?.Transition.CurrentShotCamera;
            if (shot == null) return;

            Vector3 direction = shot.Position - this.m_Player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f) return;

            this.m_FacingLayerKey = this.m_Player.Facing.SetLayerDirection(
                this.m_FacingLayerKey,
                direction.normalized,
                false
            );
        }

        private void ApplyPlayerSelfieTurnSpeed()
        {
            if (!this.m_IsActive || this.m_IsBackCameraMode ||
                !this.m_FaceCameraShot)
            {
                this.RestorePlayerTurnSpeed();
                return;
            }
            if (this.m_Player?.Motion == null) return;

            if (!this.m_HasPlayerAngularSpeedOverride ||
                this.m_TurnSpeedOwner != this.m_Player)
            {
                this.RestorePlayerTurnSpeed();
                this.m_TurnSpeedOwner = this.m_Player;
                this.m_BasePlayerAngularSpeed =
                    this.m_Player.Motion.AngularSpeed;
                this.m_HasPlayerAngularSpeedOverride = true;
            }
            else if (!Mathf.Approximately(
                this.m_Player.Motion.AngularSpeed,
                this.m_LastAppliedPlayerAngularSpeed
            ))
            {
                // A GC2 locomotion state supplied a new base turn speed.
                this.m_BasePlayerAngularSpeed =
                    this.m_Player.Motion.AngularSpeed;
            }

            float multiplier = Mathf.Max(1f, this.m_SelfieTurnSpeedMultiplier);
            float angularSpeed = this.m_BasePlayerAngularSpeed < 0f
                ? this.m_BasePlayerAngularSpeed
                : this.m_BasePlayerAngularSpeed * multiplier;
            this.m_Player.Motion.AngularSpeed = angularSpeed;
            this.m_LastAppliedPlayerAngularSpeed =
                this.m_Player.Motion.AngularSpeed;
        }

        private void RestorePlayerTurnSpeed()
        {
            if (this.m_HasPlayerAngularSpeedOverride &&
                this.m_TurnSpeedOwner?.Motion != null)
            {
                this.m_TurnSpeedOwner.Motion.AngularSpeed =
                    this.m_BasePlayerAngularSpeed;
            }

            this.m_TurnSpeedOwner = null;
            this.m_BasePlayerAngularSpeed = 0f;
            this.m_LastAppliedPlayerAngularSpeed = 0f;
            this.m_HasPlayerAngularSpeedOverride = false;
        }

        private Transform GetCameraShotLookTarget()
        {
            if (this.m_IsBackCameraMode) return null;
            if (this.m_RuntimeSelfieShot != null)
                return this.m_RuntimeSelfieShot.transform;
            return this.m_Gc2MainCamera?.Transition.CurrentShotCamera != null
                ? this.m_Gc2MainCamera.Transition.CurrentShotCamera.transform
                : null;
        }

        private void UpdatePlayerPresentationForCameraMode()
        {
            if (this.m_IsBackCameraMode)
            {
                this.ReleasePlayerFacing();
                this.RestorePlayerTurnSpeed();
                this.ApplyBackCameraPlayerPresentation();
                this.m_PhoneSystem?.SetBackCameraFastMovementEnabled(true);
                this.m_HandPresentation?.SetPhoneModelHidden(true);
                return;
            }

            this.RestoreBackCameraPlayerPresentation();
            this.m_PhoneSystem?.SetBackCameraFastMovementEnabled(false);
            this.m_HandPresentation?.SetPhoneModelHidden(false);
            this.ApplyPlayerSelfieTurnSpeed();
            this.UpdatePlayerFacing();
        }

        private void ApplyBackCameraPlayerPresentation()
        {
            if (!this.m_IsActive || !this.m_IsBackCameraMode ||
                this.m_Player == null)
            {
                this.RestoreBackCameraPlayerPresentation();
                return;
            }

            if (this.m_BackCameraHiddenPlayer != null &&
                this.m_BackCameraHiddenPlayer != this.m_Player)
            {
                this.RestoreBackCameraPlayerPresentation();
            }
            this.m_BackCameraHiddenPlayer = this.m_Player;

            // Hide only mesh renderers. The physical phone is a separate root
            // object, so it remains visible while the Player body cannot enter
            // either the main BACK view or its RenderTexture copy.
            foreach (Renderer renderer in
                     this.m_Player.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    renderer is not MeshRenderer &&
                    renderer is not SkinnedMeshRenderer)
                {
                    continue;
                }

                if (!this.m_BackCameraRendererStates.ContainsKey(renderer))
                    this.m_BackCameraRendererStates.Add(renderer, renderer.enabled);
                renderer.enabled = false;
            }

            if (this.m_BackCameraObjectDirectionToggle == null)
            {
                FranklinAnimationBridge bridge =
                    this.m_Player.GetComponentInChildren<FranklinAnimationBridge>(
                        true
                    );
                if (bridge != null)
                {
                    this.m_BackCameraObjectDirectionToggle =
                        bridge.GetComponent<FranklinObjectDirectionToggle>() ??
                        bridge.gameObject.AddComponent<
                            FranklinObjectDirectionToggle
                        >();
                }
            }

            if (this.m_BackCameraObjectDirectionToggle != null &&
                !this.m_BackCameraObjectDirectionToggle.IsObjectDirectionEnabled)
            {
                this.m_OwnsBackCameraObjectDirection =
                    this.m_BackCameraObjectDirectionToggle
                        .SetObjectDirectionEnabled(true);
            }
        }

        private void RestoreBackCameraPlayerPresentation()
        {
            foreach (KeyValuePair<Renderer, bool> state in
                     this.m_BackCameraRendererStates)
            {
                if (state.Key != null) state.Key.enabled = state.Value;
            }
            this.m_BackCameraRendererStates.Clear();
            this.m_BackCameraHiddenPlayer = null;

            if (this.m_OwnsBackCameraObjectDirection &&
                this.m_BackCameraObjectDirectionToggle != null)
            {
                this.m_BackCameraObjectDirectionToggle
                    .SetObjectDirectionEnabled(false);
            }
            this.m_OwnsBackCameraObjectDirection = false;
            this.m_BackCameraObjectDirectionToggle = null;
        }

        private void ReleasePlayerFacing()
        {
            if (this.m_Player?.Facing != null && this.m_FacingLayerKey >= 0)
                this.m_Player.Facing.DeleteLayer(this.m_FacingLayerKey);
            this.m_FacingLayerKey = -1;
        }

        private void SyncSelfieCamera()
        {
            if (this.m_MainCamera == null || this.m_SelfieCamera == null ||
                this.m_RenderTexture == null)
            {
                return;
            }

            bool shouldRender = this.m_IsActive;
            this.m_SelfieCamera.CopyFrom(this.m_MainCamera);

            // Main Camera is read-only here. GC2 has already inherited the final
            // orbit pose (including clipping/shake) from the active Camera Shot.
            Transform output = this.m_MainCamera.transform;
            this.m_SelfieCamera.transform.SetPositionAndRotation(
                output.position,
                output.rotation
            );

            this.m_SelfieCamera.targetTexture = this.m_RenderTexture;
            this.m_SelfieCamera.rect = new Rect(0f, 0f, 1f, 1f);
            this.m_SelfieCamera.depth = this.m_MainCamera.depth + 1f;
            this.ApplyRenderTextureFraming();
            this.m_SelfieCamera.nearClipPlane = this.m_SelfieNearClip;
            this.m_SelfieCamera.enabled = shouldRender;
        }

        private void ApplyRenderTextureFraming()
        {
            float renderAspect = this.m_RenderTexture.height > 0
                ? (float)this.m_RenderTexture.width / this.m_RenderTexture.height
                : 1f;
            float shotAspect = GetCameraOutputAspect(this.m_MainCamera);

            // CopyFrom can carry a projection matrix calculated for the Game
            // view. Rebuild it for the portrait target before applying its FOV.
            this.m_SelfieCamera.ResetProjectionMatrix();
            this.m_SelfieCamera.aspect = renderAspect;

            if (!this.m_MatchCameraShotHorizontalFraming)
            {
                this.m_SelfieCamera.fieldOfView = this.m_SelfieFieldOfView;
                return;
            }

            if (this.m_MainCamera.orthographic)
            {
                // Orthographic horizontal span = size * aspect.
                this.m_SelfieCamera.orthographicSize =
                    this.m_MainCamera.orthographicSize * shotAspect / renderAspect;
                return;
            }

            // Camera.fieldOfView is vertical. Convert through horizontal FOV so
            // a portrait RenderTexture retains the Camera Shot's normalized X:
            // a Player at the left edge stays at the left edge in the phone UI.
            float horizontalFieldOfView = Camera.VerticalToHorizontalFieldOfView(
                this.m_MainCamera.fieldOfView,
                shotAspect
            );
            this.m_SelfieCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(
                horizontalFieldOfView,
                renderAspect
            );
        }

        private static float GetCameraOutputAspect(Camera camera)
        {
            if (camera == null) return 1f;

            Rect pixelRect = camera.pixelRect;
            if (pixelRect.width > 0.01f && pixelRect.height > 0.01f)
                return pixelRect.width / pixelRect.height;

            if (camera.targetTexture != null && camera.targetTexture.height > 0)
            {
                return (float)camera.targetTexture.width /
                       camera.targetTexture.height;
            }

            return Mathf.Max(0.01f, camera.aspect);
        }

        private bool TryGetPhoneLensPose(
            out Vector3 lensPosition,
            out Quaternion lensRotation)
        {
            lensPosition = default;
            lensRotation = Quaternion.identity;

            Transform phone = this.m_HandPresentation != null &&
                              this.m_HandPresentation.PhoneInstance != null
                ? this.m_HandPresentation.PhoneInstance.transform
                : null;
            if (phone != null && this.m_Player != null)
            {
                lensPosition = phone.position +
                    phone.right * this.m_PhoneCameraPosition.x +
                    phone.up * this.m_PhoneCameraPosition.y +
                    phone.forward * this.m_PhoneCameraPosition.z;
                Vector3 aimPosition = this.GetPlayerAimPosition();
                Vector3 aimDirection = aimPosition - lensPosition;
                lensRotation = aimDirection.sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(
                        aimDirection.normalized,
                        this.m_Player.transform.up
                    )
                    : phone.rotation;
                lensRotation *= Quaternion.Euler(this.m_CameraRotation);
            }
            else
            {
                if (this.m_MainCamera == null) return false;
                lensPosition = this.m_MainCamera.transform.position;
                lensRotation = this.m_MainCamera.transform.rotation;
            }

            return true;
        }

        private bool TryGetBackCameraPose(
            out Vector3 position,
            out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (this.m_Player == null) return false;

            // BACK is rooted only to the stable Character transform. No hand,
            // phone socket, Animator IK or humanoid bone contributes to this
            // pose, eliminating animation feedback from the camera completely.
            Transform character = this.m_Player.transform;
            position = character.position +
                       character.right * this.m_BackCameraPosition.x +
                       character.up * this.m_BackCameraPosition.y +
                       character.forward * this.m_BackCameraPosition.z;
            Quaternion baseRotation = this.m_HasBackCameraEntryRotation
                ? this.m_BackCameraEntryRotation
                : character.rotation;
            rotation = baseRotation *
                       Quaternion.Euler(this.m_BackCameraRotation);
            return true;
        }

        private void CaptureBackCameraEntryRotation()
        {
            Transform source = this.m_MainCamera != null
                ? this.m_MainCamera.transform
                : this.m_Gc2MainCamera != null
                    ? this.m_Gc2MainCamera.transform
                    : null;
            if (source == null)
            {
                this.m_HasBackCameraEntryRotation = false;
                return;
            }

            Vector3 up = this.m_Player != null
                ? this.m_Player.transform.up
                : Vector3.up;
            this.m_BackCameraEntryRotation =
                Quaternion.AngleAxis(180f, up) * source.rotation;
            this.m_HasBackCameraEntryRotation = true;
        }

        private void EnsureMainCameraShotActive()
        {
            if (!this.m_IsActive || this.m_Gc2MainCamera == null) return;
            ShotCamera desiredShot;
            if (this.m_IsBackCameraMode)
            {
                if (!this.EnsureRuntimeBackCameraShot()) return;
                this.UpdateRuntimeBackCameraShot(false);
                desiredShot = this.m_RuntimeBackCameraShot;
            }
            else
            {
                if (!this.EnsureRuntimeSelfieShot()) return;
                desiredShot = this.m_RuntimeSelfieShot;
            }

            if (this.m_MainCameraShotOwner != null &&
                this.m_MainCameraShotOwner != this.m_Gc2MainCamera)
            {
                this.RestoreMainCameraShot();
            }

            ShotCamera currentShot =
                this.m_Gc2MainCamera.Transition.CurrentShotCamera;
            bool sessionOwned = this.m_IsMainCameraShotActive &&
                this.m_MainCameraShotOwner == this.m_Gc2MainCamera;
            bool currentIsManaged = currentShot == this.m_RuntimeSelfieShot ||
                currentShot == this.m_RuntimeBackCameraShot;

            // Capture the pre-phone shot once. If another gameplay system takes
            // control during phone use, remember that new external shot too;
            // switching SELFIE/BACK never overwrites it with our own shot.
            if (!sessionOwned || !currentIsManaged)
            {
                this.m_PreSelfieShot = currentShot;
                if (!this.m_HasPreSelfieFieldOfView ||
                    ShotControlsFieldOfView(this.m_PreSelfieShot))
                {
                    this.m_PreSelfieFieldOfView =
                        this.m_Gc2MainCamera.Viewport.FieldOfView;
                    this.m_HasPreSelfieFieldOfView = true;
                }
                this.m_MainCameraShotOwner = this.m_Gc2MainCamera;
                this.m_IsMainCameraShotActive = true;
            }

            bool ownsDesiredShot =
                this.m_Gc2MainCamera.Transition.CurrentShotCamera == desiredShot;
            if (!ownsDesiredShot)
            {
                if (this.m_IsBackCameraMode)
                {
                    this.UpdateRuntimeBackCameraShot(true);
                    this.ApplyBackCameraFraming(true);
                }
                else
                    this.ApplyCameraShotFraming(true);

                this.m_Gc2MainCamera.Transition.ChangeToShot(
                    desiredShot,
                    0f,
                    Easing.Type.Linear
                );
                if (this.m_IsBackCameraMode)
                {
                    this.UpdateRuntimeBackCameraShot(false);
                    this.ApplyBackCameraFraming(true);
                }
                else
                {
                    this.ApplyCameraShotDeviceFraming(true);
                    this.ConfigureRuntimeSelfieShotAtPhone();
                }
                this.m_Gc2MainCamera.Sync();
            }
            else if (this.m_IsBackCameraMode)
            {
                this.UpdateRuntimeBackCameraShot(false);
                this.ApplyBackCameraFraming(false);
            }
            else
            {
                this.ApplyCameraShotFraming(false);
                this.ApplyCameraShotDeviceFraming(false);
            }
        }

        private bool EnsureRuntimeBackCameraShot()
        {
            if (this.m_RuntimeBackCameraShot != null) return true;

            if (this.m_SelfieShotPrefab == null)
            {
                Debug.LogWarning(
                    "Phone Back Camera requires the configured GC2 Camera Shot prefab.",
                    this
                );
                return false;
            }

            ShotCamera shortcutMainShot = ShortcutMainShot.Get<ShotCamera>();
            this.m_RuntimeBackCameraShot = Instantiate(this.m_SelfieShotPrefab);
            ShortcutMainShot.Change(shortcutMainShot);
            GameObject shotObject = this.m_RuntimeBackCameraShot.gameObject;
            shotObject.name = "Franklin Phone Back Camera Shot";
            shotObject.hideFlags = HideFlags.HideInHierarchy;
            this.m_RuntimeBackCameraShotType =
                this.m_RuntimeBackCameraShot.ShotType as ShotTypeThirdPerson;
            this.m_RuntimeBackThirdPerson =
                this.m_RuntimeBackCameraShotType?.GetSystem(
                    ShotSystemThirdPerson.ID
                ) as ShotSystemThirdPerson;
            this.m_RuntimeBackShotViewport =
                this.m_RuntimeBackCameraShotType?.GetSystem(
                    ShotSystemViewport.ID
                ) as ShotSystemViewport;
            this.m_RuntimeBackShotFieldOfView =
                this.m_RuntimeBackShotViewport != null
                    ? SHOT_VIEWPORT_FIELD_OF_VIEW_FIELD?.GetValue(
                        this.m_RuntimeBackShotViewport
                    ) as EnablerFloat
                    : null;

            if (this.m_RuntimeBackCameraShotType == null ||
                this.m_RuntimeBackThirdPerson == null ||
                this.m_RuntimeBackShotViewport == null)
            {
                Debug.LogWarning(
                    "Phone Back Camera Shot must use GC2 Third Person and Viewport systems.",
                    this
                );
                Destroy(shotObject);
                this.m_RuntimeBackCameraShot = null;
                return false;
            }

            this.m_BackCameraPivot = new GameObject(
                "Franklin Phone Back Camera Stable Pivot"
            );
            this.m_BackCameraPivot.hideFlags = HideFlags.HideInHierarchy;
            THIRD_PERSON_PIVOT_FIELD?.SetValue(
                this.m_RuntimeBackThirdPerson,
                GetGameObjectInstance.Create(this.m_BackCameraPivot)
            );

            // The source Camera Shot also has a gameplay sprint helper. Back
            // camera orbit owns its yaw completely, so disable cloned helpers.
            foreach (MonoBehaviour behaviour in
                     shotObject.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null &&
                    behaviour != this.m_RuntimeBackCameraShot)
                {
                    behaviour.enabled = false;
                }
            }

            // The root-space pivot sits inside the Player collider even though
            // its mesh is hidden. BACK must clip through that collider so GC2
            // never moves the shot while trying to avoid its own Character.
            SHOT_CAMERA_CLIPPING_FIELD?.SetValue(
                this.m_RuntimeBackCameraShot,
                ShotCamera.Clipping.ClipThrough
            );

            this.ApplyBackCameraOrbitSettings();
            this.m_LastAppliedBackFieldOfView = -1f;
            this.m_LastBackCameraPivotFrame = -1;
            DontDestroyOnLoad(shotObject);
            DontDestroyOnLoad(this.m_BackCameraPivot);
            this.UpdateRuntimeBackCameraShot(true);
            return true;
        }

        private void UpdateRuntimeBackCameraShot(bool force)
        {
            if (this.m_RuntimeBackCameraShot == null ||
                this.m_RuntimeBackThirdPerson == null ||
                this.m_BackCameraPivot == null ||
                !this.TryGetBackCameraPose(
                    out Vector3 position,
                    out Quaternion rotation
                ))
            {
                return;
            }

            if (!force && this.m_LastBackCameraPivotFrame == Time.frameCount)
                return;
            this.m_LastBackCameraPivotFrame = Time.frameCount;

            // Direct root-space placement is stable by construction and needs
            // no bone-follow smoothing. Radius and shoulder are both zero, so
            // the shot rotates in place at this pivot.
            this.m_BackCameraPivot.transform.SetPositionAndRotation(
                position,
                rotation
            );
            if (force)
            {
                this.m_RuntimeBackThirdPerson.SetRotation(rotation);
                this.m_RuntimeBackThirdPerson.Aim(0f, 0f, 0f, 0f);
                this.m_RuntimeBackCameraShotType.Update();
            }
        }

        private void ApplyBackCameraOrbitSettings()
        {
            if (this.m_RuntimeBackCameraShotType == null ||
                this.m_RuntimeBackThirdPerson == null)
            {
                return;
            }

            SetThirdPersonDecimal(
                this.m_RuntimeBackThirdPerson,
                THIRD_PERSON_SHOULDER_FIELD,
                0f
            );
            SetThirdPersonDecimal(
                this.m_RuntimeBackThirdPerson,
                THIRD_PERSON_LIFT_FIELD,
                0f
            );
            SetThirdPersonDecimal(
                this.m_RuntimeBackThirdPerson,
                THIRD_PERSON_RADIUS_FIELD,
                0f
            );
            THIRD_PERSON_SMOOTH_TIME_FIELD?.SetValue(
                this.m_RuntimeBackThirdPerson,
                new PropertyGetDecimal(
                    Mathf.Clamp(this.m_BackCameraOrbitSmoothTime, 0f, 0.25f)
                )
            );
            if (THIRD_PERSON_MAX_YAW_FIELD?.GetValue(
                    this.m_RuntimeBackThirdPerson
                ) is EnablerAngle180 maximumYaw)
            {
                maximumYaw.IsEnabled = false;
            }
            this.m_RuntimeBackThirdPerson.Alignment.AutoAlign = false;
            ShotSystemZoom zoom = this.m_RuntimeBackCameraShotType.Zoom;
            if (zoom != null)
            {
                zoom.MinDistance = 0f;
                zoom.SmoothTime = 0f;
                // GC2 internally enforces a 0.01 minimum base radius. Zoom
                // Level zero is therefore required for an effective radius 0.
                zoom.Level = 0f;
            }
        }

        private void ApplyBackCameraFraming(bool force)
        {
            if (this.m_RuntimeBackCameraShot == null ||
                this.m_Gc2MainCamera == null)
            {
                return;
            }

            this.ApplyBackCameraOrbitSettings();

            float fieldOfView = Mathf.Clamp(
                this.m_BackCameraFieldOfView,
                35f,
                120f
            );
            bool changed = !Mathf.Approximately(
                this.m_LastAppliedBackFieldOfView,
                fieldOfView
            );
            if (this.m_RuntimeBackShotFieldOfView != null)
            {
                this.m_RuntimeBackShotFieldOfView.IsEnabled = true;
                this.m_RuntimeBackShotFieldOfView.Value = fieldOfView;
            }
            this.m_LastAppliedBackFieldOfView = fieldOfView;

            if ((!force && !changed) ||
                this.m_Gc2MainCamera.Transition.CurrentShotCamera !=
                this.m_RuntimeBackCameraShot)
            {
                return;
            }
            this.m_Gc2MainCamera.Viewport.SetFieldOfView(
                fieldOfView,
                0f,
                Easing.Type.Linear
            );
        }

        private bool EnsureRuntimeSelfieShot()
        {
            if (this.m_RuntimeSelfieShot != null) return true;

            if (this.m_SelfieShotPrefab == null)
            {
                Debug.LogWarning(
                    "Phone Camera app requires the GC2 Camera Shot prefab.",
                    this
                );
                return false;
            }

            ShotCamera shortcutMainShot = ShortcutMainShot.Get<ShotCamera>();
            this.m_RuntimeSelfieShot = Instantiate(this.m_SelfieShotPrefab);
            ShortcutMainShot.Change(shortcutMainShot);
            this.m_RuntimeSelfieShot.name = "Franklin Phone Selfie Camera Shot";
            this.m_RuntimeSelfieShotType =
                this.m_RuntimeSelfieShot.ShotType as ShotTypeThirdPerson;
            this.m_RuntimeThirdPerson = this.m_RuntimeSelfieShotType?.GetSystem(
                ShotSystemThirdPerson.ID
            ) as ShotSystemThirdPerson;
            this.m_RuntimeShotViewport =
                this.m_RuntimeSelfieShotType?.GetSystem(
                    ShotSystemViewport.ID
                ) as ShotSystemViewport;
            this.m_RuntimeShotFieldOfView = this.m_RuntimeShotViewport != null
                ? SHOT_VIEWPORT_FIELD_OF_VIEW_FIELD?.GetValue(
                    this.m_RuntimeShotViewport
                ) as EnablerFloat
                : null;
            if (this.m_RuntimeSelfieShotType == null ||
                this.m_RuntimeThirdPerson == null ||
                this.m_RuntimeShotViewport == null)
            {
                Debug.LogWarning(
                    "Phone Camera Shot must use GC2 Third Person and Viewport systems.",
                    this
                );
                Destroy(this.m_RuntimeSelfieShot.gameObject);
                this.m_RuntimeSelfieShot = null;
                return false;
            }

            this.m_HasAppliedShotFraming = false;
            this.m_LastAppliedDeviceFieldOfView = -1f;
            DontDestroyOnLoad(this.m_RuntimeSelfieShot.gameObject);
            return true;
        }

        private void ConfigureRuntimeSelfieShotAtPhone()
        {
            if (this.m_RuntimeSelfieShotType == null ||
                this.m_RuntimeThirdPerson == null || this.m_Player == null ||
                !this.TryGetPhoneLensPose(
                    out _,
                    out Quaternion lensRotation
                ))
            {
                return;
            }

            // The shot GameObject Transform is output-only for Third Person.
            // Position comes from the actual GC2 Shoulder/Lift/Radius properties.
            this.m_RuntimeThirdPerson.Aim(0f, 0f, 0f, 0f);
            this.m_RuntimeThirdPerson.SetRotation(lensRotation);
            this.m_RuntimeSelfieShotType.Update();
        }

        private void ApplyCameraShotFraming(bool force)
        {
            if (this.m_RuntimeThirdPerson == null) return;

            float radius = Mathf.Max(0.01f, this.m_CameraShotRadius);
            if (!force && this.m_HasAppliedShotFraming &&
                Mathf.Approximately(
                    this.m_LastAppliedShotShoulder,
                    this.m_CameraShotShoulder
                ) &&
                Mathf.Approximately(
                    this.m_LastAppliedShotLift,
                    this.m_CameraShotLift
                ) &&
                Mathf.Approximately(this.m_LastAppliedShotRadius, radius))
            {
                return;
            }

            SetThirdPersonDecimal(
                this.m_RuntimeThirdPerson,
                THIRD_PERSON_SHOULDER_FIELD,
                this.m_CameraShotShoulder
            );
            SetThirdPersonDecimal(
                this.m_RuntimeThirdPerson,
                THIRD_PERSON_LIFT_FIELD,
                this.m_CameraShotLift
            );
            SetThirdPersonDecimal(
                this.m_RuntimeThirdPerson,
                THIRD_PERSON_RADIUS_FIELD,
                radius
            );

            this.m_LastAppliedShotShoulder = this.m_CameraShotShoulder;
            this.m_LastAppliedShotLift = this.m_CameraShotLift;
            this.m_LastAppliedShotRadius = radius;
            this.m_HasAppliedShotFraming = true;
        }

        private void ApplyCameraShotDeviceFraming(bool force)
        {
            if (this.m_Gc2MainCamera == null || this.m_MainCamera == null ||
                this.m_RuntimeSelfieShot == null)
            {
                return;
            }

            float fieldOfView = this.m_ReferenceVerticalFieldOfView;
            if (this.m_LockHorizontalFramingAcrossDevices)
            {
                float referenceAspect = Mathf.Max(
                    0.01f,
                    this.m_ReferenceScreenSize.x /
                    Mathf.Max(1f, this.m_ReferenceScreenSize.y)
                );
                float deviceAspect = GetCameraOutputAspect(this.m_MainCamera);
                float horizontalFieldOfView =
                    Camera.VerticalToHorizontalFieldOfView(
                        this.m_ReferenceVerticalFieldOfView,
                        referenceAspect
                    );
                fieldOfView = Camera.HorizontalToVerticalFieldOfView(
                    horizontalFieldOfView,
                    deviceAspect
                );
            }

            fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
            bool changed = !Mathf.Approximately(
                this.m_LastAppliedDeviceFieldOfView,
                fieldOfView
            );

            // Keep the value on the runtime Camera Shot itself. GC2 reads this
            // viewport when changing shots; SetFieldOfView reapplies it only
            // when resolution/orientation changes while this shot is active.
            if (this.m_RuntimeShotFieldOfView != null)
            {
                this.m_RuntimeShotFieldOfView.IsEnabled = true;
                this.m_RuntimeShotFieldOfView.Value = fieldOfView;
            }

            this.m_LastAppliedDeviceFieldOfView = fieldOfView;
            if (!force && !changed) return;
            if (this.m_Gc2MainCamera.Transition.CurrentShotCamera !=
                this.m_RuntimeSelfieShot)
            {
                return;
            }

            this.m_Gc2MainCamera.Viewport.SetFieldOfView(
                fieldOfView,
                0f,
                Easing.Type.Linear
            );
        }

        private static void SetThirdPersonDecimal(
            ShotSystemThirdPerson thirdPerson,
            FieldInfo field,
            float value)
        {
            field?.SetValue(thirdPerson, new PropertyGetDecimal(value));
        }

        private void RestoreMainCameraShot()
        {
            MainCamera owner = this.m_MainCameraShotOwner;
            if (owner == null)
            {
                this.ClearMainCameraShotState();
                return;
            }

            ShotCamera currentManagedShot =
                owner.Transition.CurrentShotCamera;
            bool stillOwnsShot = currentManagedShot ==
                                 this.m_RuntimeSelfieShot ||
                                 currentManagedShot ==
                                 this.m_RuntimeBackCameraShot;
            if (stillOwnsShot)
            {
                bool restoredShotControlsFieldOfView =
                    ShotControlsFieldOfView(this.m_PreSelfieShot);
                if (this.m_PreSelfieShot != null)
                {
                    owner.Transition.ChangeToShot(
                        this.m_PreSelfieShot,
                        0f,
                        Easing.Type.Linear
                    );
                }
                else
                {
                    currentManagedShot?.OnDisableShot(owner);
                    owner.Transition.CurrentShotCamera = null;
                }
                if (this.m_PreSelfieShot != null)
                    ShortcutMainShot.Change(this.m_PreSelfieShot);
                if (!restoredShotControlsFieldOfView &&
                    this.m_HasPreSelfieFieldOfView)
                {
                    owner.Viewport.SetFieldOfView(
                        this.m_PreSelfieFieldOfView,
                        0f,
                        Easing.Type.Linear
                    );
                }
            }

            this.ClearMainCameraShotState();
        }

        private void ClearMainCameraShotState()
        {
            this.m_MainCameraShotOwner = null;
            this.m_PreSelfieShot = null;
            this.m_PreSelfieFieldOfView = 0f;
            this.m_HasPreSelfieFieldOfView = false;
            this.m_IsMainCameraShotActive = false;
        }

        private static bool ShotControlsFieldOfView(ShotCamera shot)
        {
            ShotSystemViewport viewport = shot?.ShotType?.GetSystem(
                ShotSystemViewport.ID
            ) as ShotSystemViewport;
            return viewport != null && viewport.ChangeFieldOfView;
        }

        private Vector3 GetPlayerAimPosition()
        {
            Vector3 anchor = this.m_PlayerHead != null
                ? this.m_PlayerHead.position
                : this.m_PlayerChest != null
                    ? this.m_PlayerChest.position
                    : this.m_Player.transform.position +
                      this.m_Player.transform.up * 1.55f;
            Transform player = this.m_Player.transform;
            return anchor +
                   player.right * this.m_PlayerAimOffset.x +
                   player.up * this.m_PlayerAimOffset.y +
                   player.forward * this.m_PlayerAimOffset.z;
        }

        private IEnumerator CapturePhotoRoutine()
        {
            if (this.m_ShutterButton != null)
                this.m_ShutterButton.interactable = false;
            if (this.m_StatusText != null)
                this.m_StatusText.text = "SAVING...";

            yield return new WaitForEndOfFrame();

            Texture2D capturedTexture = null;
            string savedPath = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = this.m_RenderTexture;
                capturedTexture = new Texture2D(
                    this.m_RenderTexture.width,
                    this.m_RenderTexture.height,
                    TextureFormat.RGB24,
                    false,
                    false
                );
                capturedTexture.ReadPixels(
                    new Rect(
                        0f,
                        0f,
                        this.m_RenderTexture.width,
                        this.m_RenderTexture.height
                    ),
                    0,
                    0,
                    false
                );
                capturedTexture.Apply(false, false);

                Directory.CreateDirectory(this.PhotoDirectory);
                savedPath = Path.Combine(
                    this.PhotoDirectory,
                    $"FranklinSelfie_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png"
                );
                File.WriteAllBytes(savedPath, capturedTexture.EncodeToPNG());
                this.TrimStoredPhotos();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Could not save Franklin selfie: {exception.Message}",
                    this
                );
                if (this.m_StatusText != null)
                    this.m_StatusText.text = "SAVE FAILED";
            }
            finally
            {
                RenderTexture.active = previous;
            }

            if (capturedTexture != null) Destroy(capturedTexture);

            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                this.ReloadGalleryFromDisk();
                Texture2D newest = this.m_GalleryTextures.Count > 0
                    ? this.m_GalleryTextures[0]
                    : null;
                this.EventPhotoCaptured?.Invoke(newest, savedPath);
                if (this.m_StatusText != null)
                    this.m_StatusText.text = "SAVED TO PHOTOS";
                this.StartCoroutine(this.PlayFlash());
            }

            if (this.m_ShutterButton != null)
                this.m_ShutterButton.interactable = true;
            this.m_CaptureRoutine = null;
        }

        private IEnumerator PlayFlash()
        {
            if (this.m_FlashImage != null)
                this.m_FlashImage.color = new Color(1f, 1f, 1f, 0.82f);

            float elapsed = 0f;
            const float duration = 0.20f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (this.m_FlashImage != null)
                {
                    float alpha = 0.82f *
                                  (1f - Mathf.Clamp01(elapsed / duration));
                    this.m_FlashImage.color = new Color(1f, 1f, 1f, alpha);
                }
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.75f);
            if (this.m_IsActive && this.m_StatusText != null)
                this.m_StatusText.text = "● SELFIE";
        }

        private void ReloadGalleryFromDisk()
        {
            this.DestroyGalleryTextures();

            string[] paths = GetStoredPhotoPaths(this.PhotoDirectory);
            this.StoredPhotoCount = paths.Length;
            int loadCount = Mathf.Min(
                Mathf.Max(PHOTO_SLOT_COUNT, this.m_MaxStoredPhotos),
                paths.Length
            );
            for (int i = 0; i < loadCount; ++i)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(paths[i]);
                    Texture2D texture = new(2, 2, TextureFormat.RGB24, false);
                    if (!texture.LoadImage(bytes, false))
                    {
                        Destroy(texture);
                        continue;
                    }

                    texture.name = Path.GetFileNameWithoutExtension(paths[i]);
                    texture.filterMode = this.m_FilterMode;
                    this.m_GalleryTextures.Add(texture);
                    this.m_GalleryPaths.Add(paths[i]);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"Could not load selfie '{paths[i]}': {exception.Message}",
                        this
                    );
                }
            }

            this.EnsurePhotoSlotCapacity(this.m_GalleryTextures.Count);
            for (int i = 0; i < this.m_PhotoSlots.Count; ++i)
            {
                RawImage slot = this.m_PhotoSlots[i];
                if (slot == null) continue;

                bool hasPhoto = i < this.m_GalleryTextures.Count;
                slot.texture = hasPhoto ? this.m_GalleryTextures[i] : null;
                slot.gameObject.SetActive(hasPhoto);
                if (slot.transform.parent != null)
                    slot.transform.parent.gameObject.SetActive(hasPhoto);
            }

            this.UpdatePhotoScrollLayout(
                this.m_GalleryTextures.Count,
                true
            );

            if (this.m_PhotoEmptyText != null)
            {
                this.m_PhotoEmptyText.gameObject.SetActive(
                    this.m_GalleryTextures.Count == 0
                );
                this.m_PhotoEmptyText.transform.SetAsLastSibling();
            }
        }

        private void TrimStoredPhotos()
        {
            string[] paths = GetStoredPhotoPaths(this.PhotoDirectory);
            int keepCount = Mathf.Max(PHOTO_SLOT_COUNT, this.m_MaxStoredPhotos);
            for (int i = keepCount; i < paths.Length; ++i)
            {
                try
                {
                    File.Delete(paths[i]);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"Could not remove old selfie '{paths[i]}': " +
                        exception.Message,
                        this
                    );
                }
            }
        }

        private void DestroyGalleryTextures()
        {
            if (this.m_PhotoViewerImage != null)
                this.m_PhotoViewerImage.texture = null;
            foreach (Texture2D texture in this.m_GalleryTextures)
            {
                if (texture != null) Destroy(texture);
            }
            this.m_GalleryTextures.Clear();
            this.m_GalleryPaths.Clear();
        }

        private static string[] GetStoredPhotoPaths(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return Array.Empty<string>();

            return Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
        }

        private static void SetListItem<T>(List<T> list, int index, T value)
        {
            while (list.Count <= index) list.Add(default);
            list[index] = value;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null) return null;
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; ++i)
            {
                Transform result = FindChildRecursive(root.GetChild(i), childName);
                if (result != null) return result;
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.m_CaptureWidth = Mathf.Clamp(this.m_CaptureWidth, 256, 1440);
            this.m_CaptureHeight = Mathf.Clamp(this.m_CaptureHeight, 256, 1440);
            this.m_MaxStoredPhotos = Mathf.Clamp(this.m_MaxStoredPhotos, 1, 100);
            this.m_SelfieFieldOfView = Mathf.Clamp(
                this.m_SelfieFieldOfView,
                35f,
                170f
            );
            this.m_SelfieNearClip = Mathf.Clamp(
                this.m_SelfieNearClip,
                0.01f,
                0.15f
            );
            this.m_CameraShotRadius = Mathf.Max(
                0.01f,
                this.m_CameraShotRadius
            );
            this.m_ReferenceScreenSize.x = Mathf.Max(
                1f,
                this.m_ReferenceScreenSize.x
            );
            this.m_ReferenceScreenSize.y = Mathf.Max(
                1f,
                this.m_ReferenceScreenSize.y
            );
            this.m_ReferenceVerticalFieldOfView = Mathf.Clamp(
                this.m_ReferenceVerticalFieldOfView,
                1f,
                179f
            );
            this.m_SelfieTurnSpeedMultiplier = Mathf.Max(
                1f,
                this.m_SelfieTurnSpeedMultiplier
            );
        }
#endif
    }
}
