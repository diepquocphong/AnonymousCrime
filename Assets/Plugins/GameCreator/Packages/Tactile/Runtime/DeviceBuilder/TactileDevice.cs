using UnityEngine;
using UnityEngine.InputSystem;

namespace Niam.Runtime.Tactile
{
    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
    #endif

    public class TactileDevice : InputDevice
    {
        // MEMBERS: -------------------------------------------------------------------------------

        #pragma warning disable IDE1006
        public static TactileDevice current { get; private set; }
        #pragma warning restore IDE1006

        // OVERRIDES: -----------------------------------------------------------------------------

        protected override void FinishSetup()
        {
            base.FinishSetup();
        }

        public override void MakeCurrent()
        {
            base.MakeCurrent();
            current = this;
        }

        protected override void OnRemoved()
        {
            base.OnRemoved();
            if (current == this) current = null;
        }

        // STATIC: --------------------------------------------------------------------------------

        #if UNITY_EDITOR
        static TactileDevice()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (UnityEditor.EditorApplication.isPlaying)
                    return;

                InputSystem.onAfterUpdate += Initialize;
            };
        }
        #endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            #if UNITY_EDITOR
            InputSystem.onAfterUpdate -= Initialize;
            #endif

            GeneralRepository.Get?.DeviceBuilder.BuildDeviceLayout();
        }
    }
}