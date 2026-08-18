using UnityEngine;

namespace FranklinGame.Menu
{
    /// <summary>
    /// Small, device-local handoff between the menu and the single gameplay scene.
    /// A mission is unlocked only after gameplay explicitly reports completion.
    /// </summary>
    public static class FranklinMissionProgress
    {
        public static int MissionCount => FranklinMissionCatalog.MissionCount;
        public const string HighestCompletedPreferenceKey =
            "FranklinGame.Missions.HighestCompleted.v1";
        public const string SelectedMissionPreferenceKey =
            "FranklinGame.Missions.Selected.v1";
        public const string ActiveMissionPreferenceKey =
            "FranklinGame.Missions.Active.v1";

        public static int HighestCompletedIndex => Mathf.Clamp(
            PlayerPrefs.GetInt(HighestCompletedPreferenceKey, -1),
            -1,
            MissionCount - 1
        );

        public static int SelectedMissionIndex => Mathf.Clamp(
            PlayerPrefs.GetInt(SelectedMissionPreferenceKey, 0),
            0,
            MissionCount - 1
        );

        public static int ActiveMissionIndex
        {
            get
            {
                int missionIndex = PlayerPrefs.GetInt(
                    ActiveMissionPreferenceKey,
                    -1
                );
                return IsValid(missionIndex) ? missionIndex : -1;
            }
        }

        public static bool IsUnlocked(int missionIndex)
        {
            return IsValid(missionIndex) &&
                missionIndex <= HighestCompletedIndex + 1;
        }

        public static bool IsCompleted(int missionIndex)
        {
            return IsValid(missionIndex) &&
                missionIndex <= HighestCompletedIndex;
        }

        public static bool SelectMission(int missionIndex)
        {
            if (!IsUnlocked(missionIndex)) return false;
            PlayerPrefs.SetInt(SelectedMissionPreferenceKey, missionIndex);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Captures an immutable mission handoff immediately before gameplay
        /// starts. Browsing cards changes SelectedMissionIndex but never this key.
        /// </summary>
        public static bool BeginMission(int missionIndex)
        {
            if (!IsUnlocked(missionIndex)) return false;
            PlayerPrefs.SetInt(SelectedMissionPreferenceKey, missionIndex);
            PlayerPrefs.SetInt(ActiveMissionPreferenceKey, missionIndex);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Call this only from a real gameplay success path. Selecting or starting
        /// a mission deliberately never advances progression.
        /// </summary>
        public static bool CompleteMission(int missionIndex)
        {
            if (!IsValid(missionIndex) || !IsUnlocked(missionIndex)) return false;
            // Completion is accepted only for the immutable handoff captured by
            // BeginMission. A misconfigured UnityEvent cannot skip a level.
            if (ActiveMissionIndex != missionIndex) return false;

            int previous = HighestCompletedIndex;
            int completed = Mathf.Max(previous, missionIndex);
            PlayerPrefs.SetInt(HighestCompletedPreferenceKey, completed);

            int nextMission = Mathf.Min(completed + 1, MissionCount - 1);
            PlayerPrefs.SetInt(SelectedMissionPreferenceKey, nextMission);
            if (ActiveMissionIndex == missionIndex)
            {
                // Make duplicate success events idempotent: once the active
                // mission is consumed, another callback cannot complete "next".
                PlayerPrefs.DeleteKey(ActiveMissionPreferenceKey);
            }
            PlayerPrefs.Save();
            return completed != previous;
        }

        public static bool CompleteActiveMission()
        {
            int activeMission = ActiveMissionIndex;
            return activeMission >= 0 && CompleteMission(activeMission);
        }

        public static bool CompleteSelectedMission()
        {
            return CompleteActiveMission();
        }

        public static void ClearActiveMissionHandoff(int expectedMissionIndex = -1)
        {
            int activeMission = ActiveMissionIndex;
            if (activeMission < 0) return;
            if (expectedMissionIndex >= 0 && activeMission != expectedMissionIndex) return;
            PlayerPrefs.DeleteKey(ActiveMissionPreferenceKey);
            PlayerPrefs.Save();
        }

        internal static void RestoreActiveMissionHandoff(int missionIndex)
        {
            if (IsValid(missionIndex))
            {
                PlayerPrefs.SetInt(ActiveMissionPreferenceKey, missionIndex);
            }
            else
            {
                PlayerPrefs.DeleteKey(ActiveMissionPreferenceKey);
            }
            PlayerPrefs.Save();
        }

        private static bool IsValid(int missionIndex)
        {
            return missionIndex >= 0 && missionIndex < MissionCount;
        }
    }
}
