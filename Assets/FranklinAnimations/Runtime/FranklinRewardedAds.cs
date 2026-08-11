using System;
using UnityEngine;

namespace FranklinGame.Combat
{
    /// <summary>
    /// Adapter contract for the project's rewarded-ad SDK. A LevelPlay or other provider
    /// should register itself here and only return true after the reward callback fires.
    /// </summary>
    public interface IFranklinRewardedAdProvider
    {
        bool IsRewardedVideoReady { get; }
        void ShowRewardedVideo(Action<bool> onCompleted);
    }

    public static class FranklinRewardedAds
    {
        public static IFranklinRewardedAdProvider Provider { get; set; }

        public static bool IsReady
        {
            get
            {
                if (Provider is UnityEngine.Object unityProvider && unityProvider == null)
                {
                    Provider = null;
                }
                return Provider != null && Provider.IsRewardedVideoReady;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetProvider()
        {
            Provider = null;
        }

        public static bool TryShow(Action<bool> onCompleted)
        {
            if (!IsReady) return false;
            Provider.ShowRewardedVideo(onCompleted);
            return true;
        }
    }
}
