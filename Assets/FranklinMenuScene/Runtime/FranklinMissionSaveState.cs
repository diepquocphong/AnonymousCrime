using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace FranklinGame.Menu
{
    /// <summary>
    /// Stores the active mission ID in the same GameCreator slot as gameplay.
    /// This prevents Continue from pairing an old save with a newer menu choice.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class FranklinMissionSaveState : MonoBehaviour, IGameSave
    {
        [Serializable]
        public sealed class Data
        {
            public int activeMissionIndex = -1;
        }

        private const string SaveIdentifier = "franklin-mission-active-v1";
        private const int LoadPriorityBeforeScenes = 200;
        private static FranklinMissionSaveState s_Instance;

        public string SaveID => SaveIdentifier;
        public bool IsShared => false;
        public Type SaveType => typeof(Data);
        public LoadMode LoadMode => LoadMode.Greedy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBootstrap()
        {
            s_Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            FranklinMissionSaveState existing =
                FindFirstObjectByType<FranklinMissionSaveState>(
                    FindObjectsInactive.Include
                );
            if (existing != null)
            {
                s_Instance = existing;
                DontDestroyOnLoad(existing.gameObject);
                existing.Subscribe();
                return;
            }

            GameObject owner = new("Franklin Mission Save State");
            DontDestroyOnLoad(owner);
            owner.AddComponent<FranklinMissionSaveState>();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(this.gameObject);
            this.Subscribe();
        }

        private void OnDestroy()
        {
            if (s_Instance != this) return;
            SaveLoadManager.Unsubscribe(this);
            s_Instance = null;
        }

        private void Subscribe()
        {
            _ = SaveLoadManager.Subscribe(this, LoadPriorityBeforeScenes);
        }

        public object GetSaveData(bool includeNonSavable)
        {
            return new Data
            {
                activeMissionIndex = FranklinMissionProgress.ActiveMissionIndex
            };
        }

        public Task OnLoad(object value)
        {
            // Restart() resets greedy save objects after it loads the fresh
            // gameplay scene. Ignore that reset so BeginMission's handoff is
            // preserved; a real slot load has SlotLoaded > 0 and is restored
            // before the saved scene starts creating gameplay objects.
            if (SaveLoadManager.Instance.SlotLoaded < 0)
            {
                return Task.CompletedTask;
            }

            int missionIndex = value is Data data
                ? data.activeMissionIndex
                : -1;
            FranklinMissionProgress.RestoreActiveMissionHandoff(missionIndex);
            return Task.CompletedTask;
        }
    }
}
