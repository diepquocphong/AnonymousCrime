using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;

public class AdsManager : MonoBehaviour
{
    public UnityEvent onRewardedVideoCompleted;
    private bool interstitialShown = false;
    private bool rewardedVideoShown = false;

    void Start()
    {
        if (onRewardedVideoCompleted == null)
        {
            onRewardedVideoCompleted = new UnityEvent();
        }

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            if (task.Result == DependencyStatus.Available)
            {
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                Debug.Log("Firebase Initialized");
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + task.Result);
            }
        });

        Gley.MobileAds.API.Initialize();
        Gley.MobileAds.Events.onInitialized += OnInitialized;
    }

    void OnDestroy()
    {
        Gley.MobileAds.Events.onInitialized -= OnInitialized;
        Gley.MobileAds.Events.onInterstitialLoadSucces -= onInterstitialLoadSucces;
        Gley.MobileAds.Events.onBannerLoadSucces -= OnBannerAdsShow;
    }

    public void ShowBanner()
    {
        Gley.MobileAds.API.ShowBanner(Gley.MobileAds.BannerPosition.Bottom, Gley.MobileAds.BannerType.Adaptive);
        OnAdShown("Banner");
    }

    public void ShowInterstitial()
    {
        if (Gley.MobileAds.API.IsInterstitialAvailable() && !interstitialShown)
        {
            Gley.MobileAds.API.ShowInterstitial();
            OnAdShown("Interstitial");
            StartCoroutine(InterstitialCooldown());
        }
    }

    public void ShowMRec()
    {
        Gley.MobileAds.API.ShowMRec(Gley.MobileAds.BannerPosition.BottomRight);
        OnAdShown("MRec");
    }

    public void HideMRec()
    {
        Gley.MobileAds.API.HideMRec();
    }

    private IEnumerator InterstitialCooldown()
    {
        interstitialShown = true;
        yield return new WaitForSeconds(2f); // 2 seconds cooldown
        interstitialShown = false;
    }

    public void ShowAppOpen()
    {
        if (!interstitialShown && !rewardedVideoShown)
        {
            Gley.MobileAds.API.ShowAppOpen();
            OnAdShown("AppOpen");
        }
    }

    public void ShowRewardedVideo()
    {
        if (!rewardedVideoShown)
        {
            Gley.MobileAds.API.ShowRewardedVideo(CompleteMethod);
            StartCoroutine(RewardedVideoCooldown());
        }
    }

    private IEnumerator RewardedVideoCooldown()
    {
        rewardedVideoShown = true;
        yield return new WaitForSeconds(2f); // 2 seconds cooldown
        rewardedVideoShown = false;
    }

    private void CompleteMethod(bool completed)
    {
        if (completed)
        {
            onRewardedVideoCompleted.Invoke();
            OnAdShown("RewardedVideo");
        }
    }

    private void OnInitialized()
    {
        Gley.MobileAds.Events.onInterstitialLoadSucces += onInterstitialLoadSucces;
        Gley.MobileAds.Events.onBannerLoadSucces += OnBannerAdsShow;
    }

    private void onInterstitialLoadSucces()
    {
    }

    private void OnBannerAdsShow()
    {
    }

    private void OnAdShown(string adType)
    {
        var adEventParameters = new[] {
            new Firebase.Analytics.Parameter("ad_type", adType),
            new Firebase.Analytics.Parameter("timestamp", System.DateTime.UtcNow.ToString())
        };
        Firebase.Analytics.FirebaseAnalytics.LogEvent("ad_shown", adEventParameters);
    }
}
