using System;
using System.Globalization;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.Networking;

using GameCreator.Runtime.Common;
using GameCreator.Editor.Common;

namespace Niam.Editor.Tactile
{
    using Niam.Runtime.Tactile;

    [CustomPropertyDrawer(typeof(GeneralRepository))]
    public class GeneralRepositoryDrawer : PropertyDrawer
    {
        private const string EDITOR_PATH = EditorPaths.PACKAGES + "Tactile/Editor/";
        private const string USS_PATH = EDITOR_PATH + "Stylesheets/settings";
        private const string VERSION_PATH = EDITOR_PATH + "PackageInfo/Version.txt";
        private const string CHANGELOG_PATH = EDITOR_PATH + "PackageInfo/Changelog.txt";

        private static readonly CultureInfo CULTURE = CultureInfo.InvariantCulture;
        private const int CHECK_FREQUENCY = 6;
        
        private const string KEY_CHECK_DATE = "uma:versions-check-date";
        private const string KEY_REMIND_UPDATES = "gc:versions-remind-updates";

        private const string STORE_LINK = "https://assetstore.unity.com/packages/slug/{0}";
        private const string VERSION_API = "https://api.assetstore.unity3d.com/package/latest-version/{0}";

        private const string EXPAND_MORE = "+";
        private const string EXPAND_LESS = "-";

        private const string NAME_CONTAINER_ROOT = "Tactile-Repository-Root";
        private const string NAME_CONTAINER_HEAD = "Tactile-Repository-Head";
        private const string NAME_CONTAINER_BODY = "Tactile-Repository-Body";
        private const string NAME_CONTAINER_FOOT = "Tactile-Repository-Foot";

        private static readonly IIcon ICON_UNKNOWN = new IconCircleSolid(ColorTheme.Type.Gray);
        private static readonly IIcon ICON_LATEST = new IconCircleSolid(ColorTheme.Type.Green);
        private static readonly IIcon ICON_OLD = new IconCircleSolid(ColorTheme.Type.Yellow);

        // MEMBERS: -------------------------------------------------------------------------------

        private SerializedProperty m_Property;

        private VisualElement m_Root;
        private VisualElement m_Head;
        private VisualElement m_Body;
        private VisualElement m_Foot;

        // PROPERTIES: ----------------------------------------------------------------------------

        public static bool RemindUpdates
        {
            get => EditorPrefs.GetBool(KEY_REMIND_UPDATES, true);
        }
        
        public static string Version
        {
            get
            {
                var versionTxt = AssetDatabase.LoadAssetAtPath<TextAsset>(VERSION_PATH);
                return versionTxt != null ? versionTxt.text : "Unknown";
            }
        }
        
        public static string Changelog
        {
            get
            {
                var changelog = AssetDatabase.LoadAssetAtPath<TextAsset>(CHANGELOG_PATH);
                return changelog != null ? changelog.text.Trim() : "Unknown";
            }
        }

        // PAINT: ---------------------------------------------------------------------------------

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            this.m_Property = property;

            this.m_Root = new VisualElement { name = NAME_CONTAINER_ROOT };
            this.m_Head = new VisualElement { name = NAME_CONTAINER_HEAD };
            this.m_Body = new VisualElement { name = NAME_CONTAINER_BODY };
            this.m_Foot = new VisualElement { name = NAME_CONTAINER_FOOT };

            this.RefreshHead();
            this.RefreshBody();
            this.RefreshFoot();

            this.m_Root.Add(this.m_Head);
            this.m_Root.Add(this.m_Body);
            this.m_Root.Add(this.m_Foot);

            StyleSheet[] styleSheets = StyleSheetUtils.Load(USS_PATH);
            foreach (StyleSheet sheet in styleSheets) this.m_Root.styleSheets.Add(sheet);
            
            return this.m_Root;
        }

        private void RefreshHead()
        { }

        private void RefreshBody()
        {
            var scaleMode = this.m_Property.FindPropertyRelative("m_ScaleMode");
            var dpi = this.m_Property.FindPropertyRelative("m_ReferenceDPI");
            var resolution = this.m_Property.FindPropertyRelative("m_ReferenceResolution");
            var deviceBuilder = this.m_Property.FindPropertyRelative("m_DeviceBuilder");

            var fieldScaleMode = new PropertyField(scaleMode);
            var fieldDpi = new PropertyField(dpi);
            var fieldResolution = new PropertyField(resolution);
            var fieldDeviceBuilder = new PropertyField(deviceBuilder);

            this.m_Body.Add(new LabelTitle("Coordinate Scaling"));
            this.m_Body.Add(new SpaceSmallest());
            this.m_Body.Add(fieldScaleMode);
            this.m_Body.Add(fieldResolution);
            this.m_Body.Add(fieldDpi);

            this.m_Body.Add(new SpaceSmall());
            this.m_Body.Add(fieldDeviceBuilder);

            fieldScaleMode.RegisterValueChangeCallback(_ => UpdateCoordinate());
            UpdateCoordinate();
            
            void UpdateCoordinate()
            {
                switch (scaleMode.enumValueIndex)
                {
                    case 0:
                        fieldDpi.style.display = DisplayStyle.None;
                        fieldResolution.style.display = DisplayStyle.None;
                        break;

                    case 1:
                        fieldDpi.style.display = DisplayStyle.None;
                        fieldResolution.style.display = DisplayStyle.Flex;
                        break;

                    case 2:
                        fieldDpi.style.display = DisplayStyle.Flex;
                        fieldResolution.style.display = DisplayStyle.None;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void RefreshFoot()
        {
            this.m_Foot.Add(new SpaceSmall());
            this.m_Foot.Add(new LabelTitle("Updates"));
            this.m_Foot.Add(new SpaceSmallest());

            this.DrawUpdateDrawer("Tactile", Changelog, Version, "293699");
            this.m_Foot.Add(new SpaceSmall());
        }

        private void DrawUpdateDrawer(string title, string notes, string version, string id)
        {
            var root = new VisualElement { name = "Tactile-Updates-Asset-Root" };
            var head = new VisualElement { name = "Tactile-Updates-Asset-Head" };

            var body = new ScrollView 
            { 
                name = "Tactile-Updates-Asset-Body",
                style = { display = DisplayStyle.None }
            };

            this.m_Foot.Add(root);
            root.Add(head);
            root.Add(body);

            var versionLabel = new Label(version);
            var versionCheck = new Image 
            { 
                image = version != "Unknown" ? ICON_LATEST.Texture : ICON_UNKNOWN.Texture
            };

            var btnExpand = new Button 
            {
                text = EXPAND_MORE,
                style = {
                    width = new Length(20, LengthUnit.Pixel),
                    borderRightWidth = new StyleFloat(1)
                }
            };

            btnExpand.clicked += () =>
            {
                body.style.display = body.style.display == DisplayStyle.None
                    ? DisplayStyle.Flex : DisplayStyle.None;

                btnExpand.text = body.style.display == DisplayStyle.None 
                    ? EXPAND_MORE : EXPAND_LESS;
            };

            var btnInstall = new Button 
            {
                text = "Checking...",
                style = {
                    width = new Length(100, LengthUnit.Pixel),
                    borderLeftWidth = new StyleFloat(1)
                }
            };
            btnInstall.clicked += () => Application.OpenURL(string.Format(STORE_LINK, id));
            btnInstall.SetEnabled(false);

            head.Add(btnExpand);
            head.Add(new LabelTitle(title));
            head.Add(versionLabel);
            head.Add(versionCheck);
            head.Add(btnInstall);
            body.Add(new Label(notes));

            if (Application.internetReachability == NetworkReachability.NotReachable) 
            {
                btnInstall.text = "Installed";
                return;
            }

            CheckNewVersion(id, version, (isNew, newVersion) =>
            {
                if (isNew)
                {
                    versionCheck.image = version != "Unknown" 
                        ? ICON_OLD.Texture : ICON_UNKNOWN.Texture;
                        
                    versionLabel.text = $"{version} → {newVersion}";
                    btnInstall.text = "Update";
                    btnInstall.SetEnabled(true);
                }
                else
                {
                    versionCheck.image = version != "Unknown" 
                        ? ICON_LATEST.Texture : ICON_UNKNOWN.Texture;

                    versionLabel.text = version;
                    btnInstall.text = "Installed";
                    btnInstall.SetEnabled(false);
                }
            });
        }

        // STATIC: --------------------------------------------------------------------------------

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            SettingsWindow.InitRunners.Add(new InitRunner(
                SettingsWindow.INIT_PRIORITY_DEFAULT, CanRemindUpdates, TryRemindUpdates
            ));
        }

        private static bool CanRemindUpdates()
        {
            if (!RemindUpdates) return false;
            string minDate = DateTime.MinValue.ToString(CULTURE);
            
            DateTime currentDate = DateTime.Now;
            DateTime checkDate = DateTime.Parse(
                EditorPrefs.GetString(KEY_CHECK_DATE, minDate),
                CULTURE
            );

            TimeSpan timeDifference = currentDate - checkDate;
            return timeDifference.TotalHours >= CHECK_FREQUENCY;
        }

        private static void TryRemindUpdates()
        {
            DateTime currentDate = DateTime.Now;
            EditorPrefs.SetString(KEY_CHECK_DATE, currentDate.ToString(CULTURE));

            static void OpenRepository(bool isNew, string version)
            {
                if (isNew) SettingsWindow.OpenWindow(GeneralRepository.REPOSITORY_ID);
            }

            CheckNewVersion("293699", Version, OpenRepository);
        }

        private static void CheckNewVersion(string id, string version, Action<bool, string> callback)
        {
            var www = UnityWebRequest.Get(string.Format(VERSION_API, id));
            www.SendWebRequest().completed += asyncOp => 
            {
                try
                {
                    string jsonString = www.downloadHandler.text;
                    var versionRequest = JsonUtility.FromJson<VersionRequest>(jsonString);

                    string updateVersion = versionRequest.version;
                    bool result = CompareVersions(updateVersion, version) > 0;

                    callback?.Invoke(result, updateVersion);
                }
                catch (Exception)
                { }
            };
        }

        /// <returns>
        /// returns greater than 0, if version1 is newer than version2; 
        /// returns less than 0, if version1 is older than version2; 
        /// returns 0, if version1 and version2 are the same.
        /// </returns>
        private static int CompareVersions(string version1, string version2)
        {
            string[] parts1 = Regex.Split(version1, @"(\d+|\D+)");
            string[] parts2 = Regex.Split(version2, @"(\d+|\D+)");

            int length = Math.Max(parts1.Length, parts2.Length);

            for (int i = 0; i < length; i++)
            {
                string part1 = i < parts1.Length ? parts1[i] : "";
                string part2 = i < parts2.Length ? parts2[i] : "";

                int result;
                if (int.TryParse(part1, out int num1) && int.TryParse(part2, out int num2))
                {
                    result = num1.CompareTo(num2);
                }
                else
                {
                    result = string.Compare(part1, part2, StringComparison.Ordinal);
                }

                if (result != 0) return result;
            }

            return 0;
        }
             
        ///////////////////////////////////////////////////////////////////////////////////////////
        // PRIVATE CLASS: -------------------------------------------------------------------------

        [Serializable]
        private class VersionRequest
        {
            public string version;
            public string name;
            public string category;
            public int id;
            public string publisher;
        }

    }
}