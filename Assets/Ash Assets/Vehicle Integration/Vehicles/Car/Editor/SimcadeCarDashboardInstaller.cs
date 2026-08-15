using System;
using GameCreator.Runtime.Stats;
using UnityEditor;
using UnityEngine;

namespace FranklinGame.Vehicles.Editor
{
    public static class SimcadeCarDashboardInstaller
    {
        private const string CarPrefabPath =
            "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Prefabs/Car.prefab";
        private const string FuelStationPrefabPath =
            "Assets/Ash Assets/fuel_station_mobile/fuel_station_mobile.prefab";
        private const string RadioFolder =
            "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Audio/Radio";
        private const string RadioStaticPath = RadioFolder + "/SFX/Radio_Tune_Static_CC0.mp3";
        private const string UiFolder =
            "Assets/Ash Assets/Vehicle Integration/Vehicles/Car/Textures/UI/Generated";
        private const string HudFontPath =
            "Assets/Plugins/GameCreator/Installs/GameCreator.Blockout@1.6.12/UI/_Fonts/JosefinSans-Bold.ttf";
        private const string FuelPromptIconPath =
            "Assets/UI/FranklinMobile/Resources/FranklinMobileUI/fuel-pump-icon.png";

        private static readonly string[] RadioTrackPaths =
        {
            RadioFolder + "/Stations/City_Loop_CC0.mp3",
            RadioFolder + "/Stations/Vision_CC0.mp3",
            RadioFolder + "/Stations/Iso1nhab1tans_CC0.mp3"
        };

        private static readonly string[] StationNames =
        {
            "CITY 98.7",
            "VISION 101.2",
            "ISO 104.6"
        };

        private static readonly string[] HudSpritePaths =
        {
            UiFolder + "/RadioDisc.png",
            UiFolder + "/RadioPower.png",
            UiFolder + "/RadioPrevious.png",
            UiFolder + "/RadioPlay.png",
            UiFolder + "/RadioPause.png",
            UiFolder + "/RadioNext.png",
            UiFolder + "/VehicleFuelArc.png",
            FuelPromptIconPath
        };

        [MenuItem("Tools/Franklin Game/Apply Radio And Refuel UI Only")]
        public static void ApplyRadioAndRefuelUiOnly()
        {
            AssetDatabase.Refresh();
            Sprite[] sprites = EnsureHudSprites();

            GameObject carRoot = PrefabUtility.LoadPrefabContents(CarPrefabPath);
            try
            {
                SimcadeCarDashboard dashboard =
                    RequireComponent<SimcadeCarDashboard>(carRoot);
                SerializedObject serializedDashboard = new SerializedObject(dashboard);
                serializedDashboard.FindProperty("m_RadioPauseSprite")
                    .objectReferenceValue = sprites[4];
                serializedDashboard.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dashboard);
                PrefabUtility.SaveAsPrefabAsset(carRoot, CarPrefabPath, out bool saved);
                if (!saved)
                    throw new InvalidOperationException(
                        "Could not save the redesigned Car radio UI"
                    );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(carRoot);
            }

            GameObject fuelRoot = PrefabUtility.LoadPrefabContents(FuelStationPrefabPath);
            try
            {
                FranklinFuelStation station =
                    fuelRoot.GetComponentInChildren<FranklinFuelStation>(true);
                if (station == null)
                    throw new InvalidOperationException(
                        "Fuel station prefab has no FranklinFuelStation component"
                    );

                SerializedObject serializedStation = new SerializedObject(station);
                serializedStation.FindProperty("m_ButtonPosition").vector2Value =
                    new Vector2(0f, 70f);
                serializedStation.FindProperty("m_ButtonSize").vector2Value =
                    new Vector2(620f, 96f);
                serializedStation.FindProperty("m_ButtonFontSize").intValue = 24;
                serializedStation.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(station);
                PrefabUtility.SaveAsPrefabAsset(
                    fuelRoot,
                    FuelStationPrefabPath,
                    out bool saved
                );
                if (!saved)
                    throw new InvalidOperationException(
                        "Could not save the redesigned refuel UI"
                    );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(fuelRoot);
            }

            AssetDatabase.SaveAssets();
            ValidateInstallation();
            Debug.Log(
                "Radio/refuel UI updated: one 620x96 bottom-center slot, " +
                "Play/Pause toggle, and radio suppression while refueling."
            );
        }

        public static void Install()
        {
            AudioClip[] radioTracks = EnsureRadioTracks();
            AudioClip radioStatic = EnsureRadioStatic();
            Sprite[] sprites = EnsureHudSprites();
            Font hudFont = AssetDatabase.LoadAssetAtPath<Font>(HudFontPath);
            if (hudFont == null)
                throw new InvalidOperationException("Car HUD font is missing: " + HudFontPath);

            GameObject carRoot = PrefabUtility.LoadPrefabContents(CarPrefabPath);
            try
            {
                SimcadeCarDriver driver = RequireComponent<SimcadeCarDriver>(carRoot);
                SimcadeCarImpactAudio impact = RequireComponent<SimcadeCarImpactAudio>(carRoot);
                Traits traits = RequireComponent<Traits>(carRoot);

                SimcadeCarHealth health = GetOrAdd<SimcadeCarHealth>(carRoot);
                health.Configure(traits, impact, "health-attribute-id");
                SimcadeCarFuel fuel = GetOrAdd<SimcadeCarFuel>(carRoot);
                fuel.Configure(traits, driver, "fuel-attribute-id");

                AudioSource radioSource = EnsureAudioSource(
                    carRoot,
                    "Car Radio Audio",
                    true,
                    0.55f,
                    64
                );
                AudioSource tuningSource = EnsureAudioSource(
                    carRoot,
                    "Car Radio Tuning",
                    false,
                    0.72f,
                    48
                );

                SimcadeCarDashboard dashboard = GetOrAdd<SimcadeCarDashboard>(carRoot);
                dashboard.Configure(
                    driver,
                    health,
                    fuel,
                    radioSource,
                    tuningSource,
                    radioStatic,
                    radioTracks,
                    StationNames,
                    hudFont,
                    sprites[0],
                    sprites[1],
                    sprites[2],
                    sprites[3],
                    sprites[4],
                    sprites[5],
                    sprites[6]
                );

                SerializedObject serializedDashboard = new SerializedObject(dashboard);
                serializedDashboard.FindProperty("m_SpeedWorldHeight").floatValue = 0.82f;
                serializedDashboard.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedDriver = new SerializedObject(driver);
                serializedDriver.FindProperty("m_Dashboard").objectReferenceValue = dashboard;
                serializedDriver.FindProperty("m_Fuel").objectReferenceValue = fuel;
                serializedDriver.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(health);
                EditorUtility.SetDirty(fuel);
                EditorUtility.SetDirty(dashboard);
                EditorUtility.SetDirty(radioSource);
                EditorUtility.SetDirty(tuningSource);
                PrefabUtility.SaveAsPrefabAsset(carRoot, CarPrefabPath, out bool saved);
                if (!saved)
                    throw new InvalidOperationException("Could not save Car dashboard setup");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(carRoot);
            }

            AssetDatabase.SaveAssets();
            ValidateInstallation();
        }

        public static void ValidateInstallation()
        {
            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (car == null) throw new InvalidOperationException("Car.prefab is missing");

            SimcadeCarHealth health = car.GetComponent<SimcadeCarHealth>();
            SimcadeCarFuel fuel = car.GetComponent<SimcadeCarFuel>();
            SimcadeCarDashboard dashboard = car.GetComponent<SimcadeCarDashboard>();
            SimcadeCarDriver driver = car.GetComponent<SimcadeCarDriver>();
            Traits traits = car.GetComponent<Traits>();
            if (health == null || !health.IsConfigured)
                throw new InvalidOperationException("Car health is not connected to GC2 Traits");
            if (fuel == null || !fuel.IsConfigured)
                throw new InvalidOperationException("Car fuel is not connected to GC2 Traits");
            if (dashboard == null || !dashboard.IsConfigured)
                throw new InvalidOperationException("Car dashboard/radio or generated HUD sprites are incomplete");

            SerializedObject serializedDriver = new SerializedObject(driver);
            if (serializedDriver.FindProperty("m_Dashboard").objectReferenceValue != dashboard ||
                serializedDriver.FindProperty("m_Fuel").objectReferenceValue != fuel)
            {
                throw new InvalidOperationException(
                    "SimcadeCarDriver dashboard/fuel link is missing"
                );
            }

            SerializedObject serializedDashboard = new SerializedObject(dashboard);
            if (serializedDashboard.FindProperty("m_UpdateInterval").floatValue < 0.099f ||
                serializedDashboard.FindProperty("m_WorldFollowInterval").floatValue < 0.049f ||
                serializedDashboard.FindProperty("m_SpeedFollowSmooth").floatValue < 0.03f ||
                serializedDashboard.FindProperty("m_SpeedWorldLeftOffset").floatValue < 0.5f ||
                serializedDashboard.FindProperty("m_SpeedWorldHeight").floatValue > 0.85f ||
                serializedDashboard.FindProperty("m_HealthWorldRightOffset").floatValue < 0.5f ||
                serializedDashboard.FindProperty("m_HealthWorldHeight").floatValue > 0.85f ||
                serializedDashboard.FindProperty("m_Fuel").objectReferenceValue != fuel ||
                serializedDashboard.FindProperty("m_RadioSource").objectReferenceValue is not
                    AudioSource radioSource ||
                serializedDashboard.FindProperty("m_RadioTuningSource").objectReferenceValue is not
                    AudioSource tuningSource ||
                radioSource.spatialBlend > 0.001f || radioSource.playOnAwake || !radioSource.loop ||
                tuningSource.spatialBlend > 0.001f || tuningSource.playOnAwake || tuningSource.loop)
            {
                throw new InvalidOperationException(
                    "Car dashboard world-follow/audio profile is not mobile-safe"
                );
            }

            if (dashboard.RadioTrackCount < 3)
                throw new InvalidOperationException("The three CC0 radio stations are missing");
            if (fuel.ConsumptionTickInterval < 0.2f ||
                fuel.MaximumConsumptionPerSecond <= 0f ||
                !fuel.InitializesOnFirstEnable ||
                Mathf.Abs(fuel.StartingFuel - 100f) > 0.01f)
            {
                throw new InvalidOperationException(
                    "Car fuel must start at 100 and consume at a low frequency"
                );
            }
            if (serializedDashboard.FindProperty("m_HudFont").objectReferenceValue == null)
                throw new InvalidOperationException("Josefin Sans Car HUD font is missing");
            foreach (string path in RadioTrackPaths)
                ValidateStreamingTrack(path);
            ValidateTuningClip();
            foreach (string path in HudSpritePaths)
                ValidateHudSprite(path);

            try
            {
                if (traits == null ||
                    traits.RuntimeAttributes.Get("health-attribute-id") == null ||
                    traits.RuntimeAttributes.Get("fuel-attribute-id") == null)
                {
                    throw new InvalidOperationException(
                        "GC2 health/fuel Attributes are missing from the Car Class"
                    );
                }
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "GC2 Car health/fuel Attribute validation failed",
                    exception
                );
            }

            Debug.Log(
                "Car dashboard validation passed: one shared renderer-bounds-centered HUD for all " +
                "Car instances, left fuel/right sky-blue health gauges, fuel consumption, three streamed " +
                "CC0 stations and radio tuning SFX are configured."
            );
        }

        private static AudioClip[] EnsureRadioTracks()
        {
            AudioClip[] tracks = new AudioClip[RadioTrackPaths.Length];
            for (int i = 0; i < RadioTrackPaths.Length; ++i)
            {
                string path = RadioTrackPaths[i];
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                    throw new InvalidOperationException("Radio track is missing: " + path);

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.48f;
                settings.preloadAudioData = false;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
                tracks[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (tracks[i] == null)
                    throw new InvalidOperationException("Could not import radio track: " + path);
            }
            return tracks;
        }

        private static AudioClip EnsureRadioStatic()
        {
            AudioImporter importer = AssetImporter.GetAtPath(RadioStaticPath) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("Radio tuning SFX is missing: " + RadioStaticPath);

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RadioStaticPath);
            if (clip == null)
                throw new InvalidOperationException("Could not import radio tuning SFX");
            return clip;
        }

        private static Sprite[] EnsureHudSprites()
        {
            Sprite[] sprites = new Sprite[HudSpritePaths.Length];
            for (int i = 0; i < HudSpritePaths.Length; ++i)
            {
                string path = HudSpritePaths[i];
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("Generated HUD sprite is missing: " + path);

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = path.EndsWith(
                        "VehicleFuelArc.png",
                        StringComparison.Ordinal)
                    ? 512
                    : 256;
                importer.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprites[i] == null)
                    throw new InvalidOperationException("Could not import HUD sprite: " + path);
            }
            return sprites;
        }

        private static AudioSource EnsureAudioSource(
            GameObject carRoot,
            string childName,
            bool loop,
            float volume,
            int priority)
        {
            Transform sourceTransform = carRoot.transform.Find(childName);
            if (sourceTransform == null)
            {
                sourceTransform = new GameObject(childName).transform;
                sourceTransform.SetParent(carRoot.transform, false);
            }

            AudioSource source = sourceTransform.GetComponent<AudioSource>();
            if (source == null) source = sourceTransform.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = volume;
            source.priority = priority;
            return source;
        }

        private static void ValidateStreamingTrack(string path)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            AudioImporterSampleSettings settings = importer != null
                ? importer.defaultSampleSettings
                : default;
            if (clip == null || importer == null ||
                settings.loadType != AudioClipLoadType.Streaming || settings.preloadAudioData)
            {
                throw new InvalidOperationException(
                    "Radio station must use Streaming with preload disabled: " + path
                );
            }
        }

        private static void ValidateTuningClip()
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RadioStaticPath);
            AudioImporter importer = AssetImporter.GetAtPath(RadioStaticPath) as AudioImporter;
            AudioImporterSampleSettings settings = importer != null
                ? importer.defaultSampleSettings
                : default;
            if (clip == null || importer == null ||
                settings.loadType != AudioClipLoadType.DecompressOnLoad ||
                !settings.preloadAudioData || !importer.forceToMono)
            {
                throw new InvalidOperationException("Radio tuning SFX mobile import is invalid");
            }
        }

        private static void ValidateHudSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite == null || importer == null ||
                importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Clamp)
            {
                throw new InvalidOperationException("HUD sprite import is invalid: " + path);
            }
        }

        private static T RequireComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component == null)
                throw new InvalidOperationException(
                    $"Car.prefab requires {typeof(T).Name} before dashboard setup"
                );
            return component;
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }
    }
}
