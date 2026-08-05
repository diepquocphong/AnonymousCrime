using System;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace Niam.Runtime.Tactile
{
    [Serializable]
    public class GeneralRepository : TRepository<GeneralRepository>
    {
        public enum ScaleMode
		{
			ScreenPixel,
			ScreenSize,
			ScaledPixel
		}

        // REPOSITORY PROPERTIES: -----------------------------------------------------------------

        public const string REPOSITORY_ID = "tactile.general";
        public override string RepositoryID => REPOSITORY_ID;

        // MEMBERS: -------------------------------------------------------------------------------

        [SerializeField] private ScaleMode m_ScaleMode = ScaleMode.ScreenSize;
        [SerializeField] private Vector2Int m_ReferenceResolution = new (1280, 720);
        [SerializeField] private float m_ReferenceDPI = 96f;

        [SerializeField] private DeviceBuilder m_DeviceBuilder = new DeviceBuilder();

        // PROPERTIES: ----------------------------------------------------------------------------

        public DeviceBuilder DeviceBuilder => this.m_DeviceBuilder;

		public float ScaleFactor
		{
			get
			{
                switch (this.m_ScaleMode)
                {
                    case ScaleMode.ScaledPixel:
                        float dpi = Screen.dpi;
                        return dpi > 0 ? this.m_ReferenceDPI / dpi : 1.0f;

                    case ScaleMode.ScreenPixel:
                        break;

                    case ScaleMode.ScreenSize:
                        float size = Mathf.Min(
                            Screen.width / (float)this.m_ReferenceResolution.x,
                            Screen.height / (float)this.m_ReferenceResolution.y
                        );
                        return size > 0 ? 1.0f / size : 1.0f;
                }

                return 1f;
            }
        }

        // EDITOR ENTER PLAYMODE: -----------------------------------------------------------------

        #if UNITY_EDITOR

        [UnityEditor.InitializeOnEnterPlayMode]
        public static void InitializeOnEnterPlayMode() => Instance = null;

        #endif
    }
}