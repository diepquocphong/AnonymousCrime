using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StickmanDragonFight3D.Editor
{
    internal sealed class MobileTextureAutoManagerWindow : EditorWindow
    {
        private Vector2 m_Scroll;
        private List<MobileTextureAutoManager.Entry> m_Entries = new List<MobileTextureAutoManager.Entry>();
        private MobileTextureAutoManager.Settings m_Settings;
        private string m_LastSummary = "Press Scan to audit texture import settings.";

        [MenuItem("Tools/Optimization/Texture Auto Manager/Open", false, 10)]
        private static void Open()
        {
            MobileTextureAutoManagerWindow window = GetWindow<MobileTextureAutoManagerWindow>("Texture Auto Manager");
            window.minSize = new Vector2(720f, 500f);
            window.Show();
        }

        [MenuItem("Tools/Optimization/Texture Auto Manager/Scan And Apply Mobile Profile", false, 11)]
        private static void ScanAndApplyFromMenu()
        {
            MobileTextureAutoManager.Settings settings = MobileTextureAutoManager.Settings.Load();
            List<MobileTextureAutoManager.Entry> entries = MobileTextureAutoManager.Scan(settings);
            int actionable = entries.Count(entry => entry.HasWork);

            if (actionable == 0)
            {
                EditorUtility.DisplayDialog(
                    "Texture Auto Manager",
                    "No texture needs changes with the current mobile profile.",
                    "OK"
                );
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Mobile Texture Profile",
                    $"Apply changes to {actionable} texture(s)?\n\n" +
                    "PNG/JPG source files larger than the limit can be resized. " +
                    "Other formats will only receive Unity import caps.",
                    "Apply",
                    "Cancel"))
            {
                return;
            }

            MobileTextureAutoManager.Apply(settings, entries);
        }

        [MenuItem("Tools/Optimization/Texture Auto Manager/Auto Import Guard Enabled", false, 40)]
        private static void ToggleAutoImportGuard()
        {
            MobileTextureAutoManager.SetAutoImportGuardEnabled(
                !MobileTextureAutoManager.AutoImportGuardEnabled
            );
        }

        [MenuItem("Tools/Optimization/Texture Auto Manager/Auto Import Guard Enabled", true)]
        private static bool ToggleAutoImportGuardValidate()
        {
            Menu.SetChecked(
                "Tools/Optimization/Texture Auto Manager/Auto Import Guard Enabled",
                MobileTextureAutoManager.AutoImportGuardEnabled
            );
            return true;
        }

        private void OnEnable()
        {
            m_Settings = MobileTextureAutoManager.Settings.Load();
        }

        private void OnDisable()
        {
            m_Settings.Save();
        }

        private void OnGUI()
        {
            if (m_Settings == null)
            {
                m_Settings = MobileTextureAutoManager.Settings.Load();
            }

            EditorGUILayout.LabelField("Mobile Texture Auto Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scans texture imports, skips UI/editor/skybox by default, caps runtime textures for mobile, and can resize PNG/JPG source files safely.",
                MessageType.Info
            );

            DrawSettings();
            DrawActions();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(m_LastSummary, EditorStyles.wordWrappedLabel);

            DrawResults();
        }

        private void DrawSettings()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                DefaultAsset rootAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(m_Settings.RootPath);
                DefaultAsset nextRoot = EditorGUILayout.ObjectField(
                    "Root Folder",
                    rootAsset,
                    typeof(DefaultAsset),
                    false
                ) as DefaultAsset;

                if (nextRoot != rootAsset && nextRoot != null)
                {
                    string path = AssetDatabase.GetAssetPath(nextRoot);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        m_Settings.RootPath = path;
                    }
                }
            }

            m_Settings.MaxTextureSize = EditorGUILayout.IntPopup(
                "Max Import Size",
                m_Settings.MaxTextureSize,
                new[] { "256", "512", "1024" },
                new[] { 256, 512, 1024 }
            );

            m_Settings.ResizeSourceFiles = EditorGUILayout.Toggle(
                "Resize PNG/JPG Sources",
                m_Settings.ResizeSourceFiles
            );
            using (new EditorGUI.DisabledScope(!m_Settings.ResizeSourceFiles))
            {
                m_Settings.HalveSourceBeforeCap = EditorGUILayout.Toggle(
                    "Resize To Half, Cap Max",
                    m_Settings.HalveSourceBeforeCap
                );
                m_Settings.JpegQuality = EditorGUILayout.IntSlider("JPG Quality", m_Settings.JpegQuality, 60, 95);
            }

            m_Settings.ForceMobilePlatformOverrides = EditorGUILayout.Toggle(
                "Android/iOS Overrides",
                m_Settings.ForceMobilePlatformOverrides
            );
            m_Settings.CompressUncompressedTextures = EditorGUILayout.Toggle(
                "Compress Uncompressed",
                m_Settings.CompressUncompressedTextures
            );
            m_Settings.ClampAnisoToOne = EditorGUILayout.Toggle(
                "Clamp Aniso To 1",
                m_Settings.ClampAnisoToOne
            );
            m_Settings.EnableStreamingMipmaps = EditorGUILayout.Toggle(
                "Streaming Mipmaps",
                m_Settings.EnableStreamingMipmaps
            );

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Skip Rules", EditorStyles.boldLabel);
            m_Settings.SkipUiTextures = EditorGUILayout.Toggle("Skip UI/Sprites", m_Settings.SkipUiTextures);
            m_Settings.SkipSkyboxTextures = EditorGUILayout.Toggle("Skip Skybox/Cubemap", m_Settings.SkipSkyboxTextures);
            m_Settings.SkipEditorTextures = EditorGUILayout.Toggle("Skip Editor/Gizmos", m_Settings.SkipEditorTextures);

            bool autoGuard = MobileTextureAutoManager.AutoImportGuardEnabled;
            bool nextAutoGuard = EditorGUILayout.Toggle("Auto Import Guard", autoGuard);
            if (nextAutoGuard != autoGuard)
            {
                MobileTextureAutoManager.SetAutoImportGuardEnabled(nextAutoGuard);
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan", GUILayout.Height(28f)))
                {
                    Scan();
                }

                using (new EditorGUI.DisabledScope(m_Entries.Count == 0 || !m_Entries.Any(entry => entry.HasWork)))
                {
                    if (GUILayout.Button("Apply To Scan Result", GUILayout.Height(28f)))
                    {
                        ApplyCurrentScan();
                    }
                }

                if (GUILayout.Button("Scan + Apply", GUILayout.Height(28f)))
                {
                    Scan();
                    if (m_Entries.Any(entry => entry.HasWork))
                    {
                        ApplyCurrentScan();
                    }
                }
            }
        }

        private void DrawResults()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Scan Result", EditorStyles.boldLabel);

            if (m_Entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No scan result yet.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Size", GUILayout.Width(92f));
                GUILayout.Label("Action", GUILayout.Width(210f));
                GUILayout.Label("Texture");
            }

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            int shown = 0;
            foreach (MobileTextureAutoManager.Entry entry in m_Entries)
            {
                if (!entry.HasWork && shown > 300)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{entry.Width}x{entry.Height}", GUILayout.Width(92f));
                    GUILayout.Label(entry.ActionSummary, GUILayout.Width(210f));
                    EditorGUILayout.LabelField(entry.Path);
                }

                shown++;
            }
            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            m_Settings.Save();
            m_Entries = MobileTextureAutoManager.Scan(m_Settings);
            int actionable = m_Entries.Count(entry => entry.HasWork);
            int resize = m_Entries.Count(entry => (entry.Actions & MobileTextureAutoManager.Action.ResizeSource) != 0);
            m_LastSummary = $"Scanned {m_Entries.Count} texture(s). Pending changes: {actionable}. Source resize: {resize}.";
        }

        private void ApplyCurrentScan()
        {
            int actionable = m_Entries.Count(entry => entry.HasWork);
            if (actionable == 0)
            {
                m_LastSummary = "Nothing to apply.";
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Texture Optimization",
                    $"Apply mobile profile to {actionable} texture(s)?",
                    "Apply",
                    "Cancel"))
            {
                return;
            }

            MobileTextureAutoManager.Report report = MobileTextureAutoManager.Apply(m_Settings, m_Entries);
            m_LastSummary =
                $"Applied. Import settings: {report.ImportSettingsChanged}. " +
                $"Source resized: {report.SourceFilesResized}. Skipped resize: {report.SourceResizeSkipped}.";
            Scan();
        }
    }

    internal sealed class MobileTextureAutoImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!MobileTextureAutoManager.AutoImportGuardEnabled ||
                assetImporter is not TextureImporter importer)
            {
                return;
            }

            MobileTextureAutoManager.Settings settings = MobileTextureAutoManager.Settings.Load();
            MobileTextureAutoManager.ApplyImporterSettings(importer, assetPath, settings);
        }
    }

    internal static class MobileTextureAutoManager
    {
        [Flags]
        internal enum Action
        {
            None = 0,
            ResizeSource = 1,
            CapImporter = 2,
            Compress = 4,
            MobileOverrides = 8,
            StreamingMipmaps = 16,
            ClampAniso = 32
        }

        internal sealed class Entry
        {
            public string Path;
            public int Width;
            public int Height;
            public int TargetWidth;
            public int TargetHeight;
            public Action Actions;
            public string Note;

            public bool HasWork => Actions != Action.None;

            public string ActionSummary
            {
                get
                {
                    if (!HasWork) return "OK";

                    List<string> parts = new List<string>();
                    if ((Actions & Action.ResizeSource) != 0) parts.Add($"resize {TargetWidth}x{TargetHeight}");
                    if ((Actions & Action.CapImporter) != 0) parts.Add("cap import");
                    if ((Actions & Action.Compress) != 0) parts.Add("compress");
                    if ((Actions & Action.MobileOverrides) != 0) parts.Add("mobile override");
                    if ((Actions & Action.StreamingMipmaps) != 0) parts.Add("mip streaming");
                    if ((Actions & Action.ClampAniso) != 0) parts.Add("aniso 1");
                    if (!string.IsNullOrEmpty(Note)) parts.Add(Note);
                    return string.Join(", ", parts);
                }
            }
        }

        internal sealed class Report
        {
            public int SourceFilesResized;
            public int SourceResizeSkipped;
            public int ImportSettingsChanged;
        }

        internal sealed class Settings
        {
            private const string Prefix = "StickmanDragonFight3D.MobileTextureAutoManager.";

            public string RootPath = "Assets";
            public int MaxTextureSize = 1024;
            public bool ResizeSourceFiles = true;
            public bool HalveSourceBeforeCap = true;
            public int JpegQuality = 86;
            public bool ForceMobilePlatformOverrides = true;
            public bool CompressUncompressedTextures = true;
            public bool ClampAnisoToOne = true;
            public bool EnableStreamingMipmaps = true;
            public bool SkipUiTextures = true;
            public bool SkipSkyboxTextures = true;
            public bool SkipEditorTextures = true;

            public static Settings Load()
            {
                Settings settings = new Settings
                {
                    RootPath = EditorPrefs.GetString(Prefix + nameof(RootPath), "Assets"),
                    MaxTextureSize = EditorPrefs.GetInt(Prefix + nameof(MaxTextureSize), 1024),
                    ResizeSourceFiles = EditorPrefs.GetBool(Prefix + nameof(ResizeSourceFiles), true),
                    HalveSourceBeforeCap = EditorPrefs.GetBool(Prefix + nameof(HalveSourceBeforeCap), true),
                    JpegQuality = EditorPrefs.GetInt(Prefix + nameof(JpegQuality), 86),
                    ForceMobilePlatformOverrides = EditorPrefs.GetBool(Prefix + nameof(ForceMobilePlatformOverrides), true),
                    CompressUncompressedTextures = EditorPrefs.GetBool(Prefix + nameof(CompressUncompressedTextures), true),
                    ClampAnisoToOne = EditorPrefs.GetBool(Prefix + nameof(ClampAnisoToOne), true),
                    EnableStreamingMipmaps = EditorPrefs.GetBool(Prefix + nameof(EnableStreamingMipmaps), true),
                    SkipUiTextures = EditorPrefs.GetBool(Prefix + nameof(SkipUiTextures), true),
                    SkipSkyboxTextures = EditorPrefs.GetBool(Prefix + nameof(SkipSkyboxTextures), true),
                    SkipEditorTextures = EditorPrefs.GetBool(Prefix + nameof(SkipEditorTextures), true)
                };

                settings.MaxTextureSize = Mathf.Clamp(settings.MaxTextureSize, 32, 1024);
                settings.JpegQuality = Mathf.Clamp(settings.JpegQuality, 1, 100);
                if (!AssetDatabase.IsValidFolder(settings.RootPath)) settings.RootPath = "Assets";
                return settings;
            }

            public void Save()
            {
                EditorPrefs.SetString(Prefix + nameof(RootPath), RootPath);
                EditorPrefs.SetInt(Prefix + nameof(MaxTextureSize), MaxTextureSize);
                EditorPrefs.SetBool(Prefix + nameof(ResizeSourceFiles), ResizeSourceFiles);
                EditorPrefs.SetBool(Prefix + nameof(HalveSourceBeforeCap), HalveSourceBeforeCap);
                EditorPrefs.SetInt(Prefix + nameof(JpegQuality), JpegQuality);
                EditorPrefs.SetBool(Prefix + nameof(ForceMobilePlatformOverrides), ForceMobilePlatformOverrides);
                EditorPrefs.SetBool(Prefix + nameof(CompressUncompressedTextures), CompressUncompressedTextures);
                EditorPrefs.SetBool(Prefix + nameof(ClampAnisoToOne), ClampAnisoToOne);
                EditorPrefs.SetBool(Prefix + nameof(EnableStreamingMipmaps), EnableStreamingMipmaps);
                EditorPrefs.SetBool(Prefix + nameof(SkipUiTextures), SkipUiTextures);
                EditorPrefs.SetBool(Prefix + nameof(SkipSkyboxTextures), SkipSkyboxTextures);
                EditorPrefs.SetBool(Prefix + nameof(SkipEditorTextures), SkipEditorTextures);
            }
        }

        private const string AutoImportGuardKey = "StickmanDragonFight3D.MobileTextureAutoManager.AutoImportGuard";
        private const string ReportPath = "Library/MobileTextureAutoManager.report.txt";

        private static readonly string[] UiTokens =
        {
            "/ui/",
            "/uis/",
            "/gui/",
            "/hud/",
            "/icon",
            "/icons/",
            " icons/",
            "/button",
            "/buttons/",
            "/sprite",
            "/sprites/",
            "/font",
            "/fonts/",
            "textmesh pro"
        };

        private static readonly string[] SkyboxTokens =
        {
            "skybox",
            "cubemap",
            "/boxophobic/"
        };

        private static readonly string[] EditorTokens =
        {
            "/editor/",
            "/editor default resources/",
            "/gizmos/",
            "/documentation",
            "/manual/"
        };

        public static bool AutoImportGuardEnabled =>
            EditorPrefs.GetBool(AutoImportGuardKey, false);

        public static void SetAutoImportGuardEnabled(bool enabled)
        {
            EditorPrefs.SetBool(AutoImportGuardKey, enabled);
            Menu.SetChecked(
                "Tools/Optimization/Texture Auto Manager/Auto Import Guard Enabled",
                enabled
            );
        }

        internal static List<Entry> Scan(Settings settings)
        {
            string[] roots = { settings.RootPath };
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", roots);
            List<Entry> entries = new List<Entry>(guids.Length);

            foreach (string guid in guids.Distinct())
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || ShouldSkip(path, importer, settings)) continue;

                Entry entry = BuildEntry(path, importer, settings);
                entries.Add(entry);
            }

            return entries
                .OrderByDescending(entry => entry.HasWork)
                .ThenByDescending(entry => Mathf.Max(entry.Width, entry.Height))
                .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static Report Apply(Settings settings, IReadOnlyList<Entry> entries)
        {
            Report report = new Report();
            int total = entries.Count(entry => entry.HasWork);
            int index = 0;

            try
            {
                foreach (Entry entry in entries)
                {
                    if (!entry.HasWork) continue;

                    index++;
                    EditorUtility.DisplayProgressBar(
                        "Texture Auto Manager",
                        entry.Path,
                        total <= 0 ? 1f : index / (float)total
                    );

                    if ((entry.Actions & Action.ResizeSource) != 0)
                    {
                        if (ResizeSourceTexture(entry.Path, entry.TargetWidth, entry.TargetHeight, settings.JpegQuality))
                        {
                            report.SourceFilesResized++;
                            AssetDatabase.ImportAsset(entry.Path, ImportAssetOptions.ForceUpdate);
                        }
                        else
                        {
                            report.SourceResizeSkipped++;
                        }
                    }

                    TextureImporter importer = AssetImporter.GetAtPath(entry.Path) as TextureImporter;
                    if (importer == null) continue;

                    if (ApplyImporterSettings(importer, entry.Path, settings))
                    {
                        importer.SaveAndReimport();
                        report.ImportSettingsChanged++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteReport(settings, report, entries);
            Debug.Log(
                $"[Texture Auto Manager] Applied mobile texture profile. " +
                $"Import settings changed: {report.ImportSettingsChanged}. " +
                $"Source files resized: {report.SourceFilesResized}. " +
                $"Resize skipped: {report.SourceResizeSkipped}."
            );

            return report;
        }

        internal static bool ApplyImporterSettings(TextureImporter importer, string path, Settings settings)
        {
            if (ShouldSkip(path, importer, settings)) return false;

            bool changed = false;
            int maxSize = Mathf.Clamp(settings.MaxTextureSize, 32, 1024);

            if (importer.maxTextureSize > maxSize)
            {
                importer.maxTextureSize = maxSize;
                changed = true;
            }

            if (settings.CompressUncompressedTextures &&
                importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                changed = true;
            }

            if (settings.ClampAnisoToOne && importer.anisoLevel > 1)
            {
                importer.anisoLevel = 1;
                changed = true;
            }

            if (settings.EnableStreamingMipmaps &&
                importer.mipmapEnabled &&
                !importer.streamingMipmaps &&
                SupportsStreamingMipmaps(importer))
            {
                importer.streamingMipmaps = true;
                changed = true;
            }

            if (settings.ForceMobilePlatformOverrides)
            {
                changed |= ApplyPlatformSettings(importer, "Android", maxSize);
                changed |= ApplyPlatformSettings(importer, "iPhone", maxSize);
            }

            return changed;
        }

        private static Entry BuildEntry(string path, TextureImporter importer, Settings settings)
        {
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);

            Entry entry = new Entry
            {
                Path = path,
                Width = width,
                Height = height
            };

            int sourceMax = Mathf.Max(width, height);
            int maxSize = Mathf.Clamp(settings.MaxTextureSize, 32, 1024);

            if (settings.ResizeSourceFiles &&
                sourceMax > maxSize &&
                CanResizeSourceFile(path))
            {
                int targetMax = settings.HalveSourceBeforeCap
                    ? Mathf.Min(maxSize, Mathf.Max(1, sourceMax / 2))
                    : maxSize;

                float scale = targetMax / (float)sourceMax;
                entry.TargetWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
                entry.TargetHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
                entry.Actions |= Action.ResizeSource;
            }
            else if (settings.ResizeSourceFiles && sourceMax > maxSize)
            {
                entry.Note = "source cap only";
            }

            if (importer.maxTextureSize > maxSize)
            {
                entry.Actions |= Action.CapImporter;
            }

            if (settings.CompressUncompressedTextures &&
                importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                entry.Actions |= Action.Compress;
            }

            if (settings.ClampAnisoToOne && importer.anisoLevel > 1)
            {
                entry.Actions |= Action.ClampAniso;
            }

            if (settings.EnableStreamingMipmaps &&
                importer.mipmapEnabled &&
                !importer.streamingMipmaps &&
                SupportsStreamingMipmaps(importer))
            {
                entry.Actions |= Action.StreamingMipmaps;
            }

            if (settings.ForceMobilePlatformOverrides &&
                (NeedsPlatformSettings(importer, "Android", maxSize) ||
                 NeedsPlatformSettings(importer, "iPhone", maxSize)))
            {
                entry.Actions |= Action.MobileOverrides;
            }

            return entry;
        }

        private static bool ApplyPlatformSettings(TextureImporter importer, string platform, int maxSize)
        {
            TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings(platform);
            bool changed = false;

            if (platformSettings.name != platform)
            {
                platformSettings.name = platform;
                changed = true;
            }

            if (!platformSettings.overridden)
            {
                platformSettings.overridden = true;
                changed = true;
            }

            if (platformSettings.maxTextureSize <= 0 || platformSettings.maxTextureSize > maxSize)
            {
                platformSettings.maxTextureSize = maxSize;
                changed = true;
            }

            if (platformSettings.format != TextureImporterFormat.Automatic)
            {
                platformSettings.format = TextureImporterFormat.Automatic;
                changed = true;
            }

            if (platformSettings.textureCompression == TextureImporterCompression.Uncompressed)
            {
                platformSettings.textureCompression = TextureImporterCompression.Compressed;
                changed = true;
            }

            if (platformSettings.compressionQuality > 50)
            {
                platformSettings.compressionQuality = 50;
                changed = true;
            }

            if (platformSettings.crunchedCompression)
            {
                platformSettings.crunchedCompression = false;
                changed = true;
            }

            if (changed)
            {
                importer.SetPlatformTextureSettings(platformSettings);
            }

            return changed;
        }

        private static bool NeedsPlatformSettings(TextureImporter importer, string platform, int maxSize)
        {
            TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings(platform);
            return !platformSettings.overridden ||
                   platformSettings.maxTextureSize <= 0 ||
                   platformSettings.maxTextureSize > maxSize ||
                   platformSettings.format != TextureImporterFormat.Automatic ||
                   platformSettings.textureCompression == TextureImporterCompression.Uncompressed ||
                   platformSettings.compressionQuality > 50 ||
                   platformSettings.crunchedCompression;
        }

        private static bool SupportsStreamingMipmaps(TextureImporter importer)
        {
            return importer.textureShape == TextureImporterShape.Texture2D &&
                   (importer.textureType == TextureImporterType.Default ||
                    importer.textureType == TextureImporterType.NormalMap ||
                    importer.textureType == TextureImporterType.SingleChannel);
        }

        private static bool ShouldSkip(string path, TextureImporter importer, Settings settings)
        {
            if (importer.textureShape != TextureImporterShape.Texture2D) return true;

            if (settings.SkipUiTextures &&
                (ContainsAny(path, UiTokens) || importer.textureType == TextureImporterType.Sprite))
            {
                return true;
            }

            if (settings.SkipSkyboxTextures && ContainsAny(path, SkyboxTokens))
            {
                return true;
            }

            if (settings.SkipEditorTextures && ContainsAny(path, EditorTokens))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsAny(string path, IReadOnlyList<string> tokens)
        {
            string searchable = "/" + path.Replace('\\', '/').ToLowerInvariant();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (searchable.Contains(tokens[i])) return true;
            }

            return false;
        }

        private static bool CanResizeSourceFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg";
        }

        private static bool ResizeSourceTexture(string assetPath, int targetWidth, int targetHeight, int jpegQuality)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!File.Exists(fullPath)) return false;

            byte[] bytes = File.ReadAllBytes(fullPath);
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D resized = null;

            try
            {
                if (!source.LoadImage(bytes, false)) return false;

                resized = ResizeTexture(source, targetWidth, targetHeight);
                byte[] output;
                string extension = Path.GetExtension(assetPath).ToLowerInvariant();
                if (extension == ".jpg" || extension == ".jpeg")
                {
                    output = resized.EncodeToJPG(Mathf.Clamp(jpegQuality, 1, 100));
                }
                else
                {
                    output = resized.EncodeToPNG();
                }

                File.WriteAllBytes(fullPath, output);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (resized != null) UnityEngine.Object.DestroyImmediate(resized);
            }
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default
            );

            RenderTexture previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                Texture2D resized = new Texture2D(width, height, TextureFormat.RGBA32, false);
                resized.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                resized.Apply(false, false);
                return resized;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static void WriteReport(
            Settings settings,
            Report report,
            IReadOnlyList<Entry> entries)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            IEnumerable<Entry> changed = entries.Where(entry => entry.HasWork);
            string text =
                $"UTC: {DateTime.UtcNow:O}\n" +
                $"Root: {settings.RootPath}\n" +
                $"Max import size: {settings.MaxTextureSize}\n" +
                $"Source files resized: {report.SourceFilesResized}\n" +
                $"Source resize skipped: {report.SourceResizeSkipped}\n" +
                $"Import settings changed: {report.ImportSettingsChanged}\n\n" +
                string.Join(
                    "\n",
                    changed.Select(entry =>
                        $"{entry.Path} | {entry.Width}x{entry.Height} | {entry.ActionSummary}")
                );

            File.WriteAllText(fullPath, text);
        }
    }
}
