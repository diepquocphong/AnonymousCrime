using UnityEditor;
using GameCreator.Editor.Installs;

namespace Niam.Editor.Tactile 
{
    public static class UninstallTactile
    {
        [MenuItem(
            itemName: "Game Creator/Uninstall/Tactile",
            isValidateFunction: false,
            priority: UninstallManager.PRIORITY
        )]
        
        private static void Uninstall()
        {
            UninstallManager.Uninstall("Tactile");
        }
    }
}