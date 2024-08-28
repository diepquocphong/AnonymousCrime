using System;
using GameCreator.Runtime.Common;

namespace GameCreator.Tools.Runtime.Footsteps
{
    [Serializable]
    public class FootstepRepository : TRepository<FootstepRepository>
    {
        public const string REPOSITORY_ID = "core.footsteps";

        private const string KEY_MODEL_PATH = "gc:footsteps-model-path";

        // REPOSITORY PROPERTIES: -----------------------------------------------------------------
        
        public override string RepositoryID => REPOSITORY_ID;
        
        // PROPERTIES: ----------------------------------------------------------------------------

        #if UNITY_EDITOR
        
        public static string EditorModelPath
        {
            get => UnityEditor.EditorPrefs.GetString(KEY_MODEL_PATH, string.Empty);
            set => UnityEditor.EditorPrefs.SetString(KEY_MODEL_PATH, value);
        }
        
        #endif
    }
}