using UnityEngine;

namespace FranklinGame.Menu
{
    /// <summary>
    /// UnityEvent-friendly endpoint for a real gameplay success event. Add this
    /// component to the mission director and invoke CompleteActiveMission only
    /// after the active mission has genuinely succeeded.
    /// </summary>
    [AddComponentMenu("Frank Sandbox Game/Missions/Mission Completion Bridge")]
    [DisallowMultipleComponent]
    public sealed class FranklinMissionCompletionBridge : MonoBehaviour
    {
        public void CompleteSelectedMission()
        {
            FranklinMissionProgress.CompleteActiveMission();
        }

        public void CompleteActiveMission()
        {
            FranklinMissionProgress.CompleteActiveMission();
        }

        public void CompleteMission(int missionIndex)
        {
            FranklinMissionProgress.CompleteMission(missionIndex);
        }
    }
}
