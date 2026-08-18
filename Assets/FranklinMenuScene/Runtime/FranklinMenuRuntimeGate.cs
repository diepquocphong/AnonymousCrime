using UnityEngine.SceneManagement;

namespace FranklinGame.Menu
{
    /// <summary>
    /// Shared scene predicate used by gameplay-only runtime bootstraps. This
    /// prevents large persistent gameplay systems from being created in the menu.
    /// </summary>
    public static class FranklinMenuRuntimeGate
    {
        private const string MenuSceneName = "FranklinMenuScene";

        public static bool IsMenuSceneActive
        {
            get
            {
                Scene scene = SceneManager.GetActiveScene();
                return scene.IsValid() && scene.name == MenuSceneName;
            }
        }
    }
}
