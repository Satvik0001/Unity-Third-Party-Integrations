using System.Collections.Generic;

namespace TPIntegrations.Editor
{
    /// <summary>
    /// What "uses this SDK" means for each integration (keyed by Integration.Id).
    /// Pattern  = regex matched against the CODE part of a line (strings and comments are ignored).
    ///            A line that matches is commented out by CodeCleaner.
    /// Asmdefs  = assembly-definition names (trailing * = prefix) to drop from "references" of your own .asmdef files.
    /// Add an entry here whenever you add an SDK to IntegrationCatalog.
    /// </summary>
    public static class CleanupRules
    {
        public struct Rule
        {
            public string Pattern;
            public string[] Asmdefs;
            public Rule(string pattern, params string[] asmdefs) { Pattern = pattern; Asmdefs = asmdefs; }
        }

        static readonly Dictionary<string, Rule> rules = new Dictionary<string, Rule>
        {
            // ---- Ads & Mediation
            // Matches SDK classes as well as common ad variable instances (case-insensitively)
            { "admob", new Rule(@"GoogleMobileAds|\b(?:BannerView|InterstitialAd|RewardedAd|RewardedInterstitialAd|AppOpenAd|AdRequest|MobileAds|ConsentInformation|ConsentForm|ConsentRequestParameters|bannerView|interstitialAd|rewardedAd|rewardedInterstitialAd|appOpenAd|adRequest|adView)\b", "GoogleMobileAds*") },
            { "applovin", new Rule(@"\bMaxSdk\w*|\bAppLovin\w*") },
            { "levelplay", new Rule(@"Unity\.Services\.LevelPlay|\bLevelPlay\w*|\bIronSource\w*|com\.unity3d\.mediation", "Unity.Services.LevelPlay*") },
            { "topon", new Rule(@"\bAnyThink\w*|\bATSDKAPI\b|\bAT(?:Banner|Interstitial|Rewarded|Native|Splash)\w*") },

            // ---- Privacy
            { "att", new Rule(@"\bATTrackingStatusBinding\b|Unity\.Advertisement\.IosSupport", "Unity.Advertisement.IosSupport*") },

            // ---- Analytics & Attribution
            { "firebase", new Rule(@"\bFirebase\w*|\bCrashlytics\b") },
            { "appsflyer", new Rule(@"\bAppsFlyer\w*") },
            { "adjust", new Rule(@"com\.adjust\.sdk|\bAdjustSdk\b|\bAdjust(?:Config|Event|Attribution|Environment|LogLevel|Android|AdRevenue|ThirdPartySharing|Purchase\w*|\w*Delegate)\b|\bAdjust\s*\.\s*[A-Za-z]") },
            { "unity-analytics", new Rule(@"Unity\.Services\.Analytics|\bAnalyticsService\b", "Unity.Services.Analytics*") },

            // ---- IAP
            { "unity-iap", new Rule(@"UnityEngine\.Purchasing|\b(?:IStoreListener|IDetailedStoreListener|IStoreController|IExtensionProvider|ConfigurationBuilder|StandardPurchasingModule|UnityPurchasing|CodelessIAPStoreListener|IAPButton|PurchaseEventArgs|PurchaseProcessingResult)\b", "Unity.Purchasing*", "UnityEngine.Purchasing*") },

            // ---- Haptics / Feel
            { "haptic-feedback", new Rule(@"CandyCoded\.HapticFeedback|\bHapticFeedback\b", "CandyCoded.HapticFeedback*") },
            { "feel", new Rule(@"MoreMountains|\bMMF_\w+|\bMMFeedbacks\w*|\bMMVibration\w*|\bMMEventManager\b|\bMMEventListener\b", "MoreMountains*") },
            { "dotween", new Rule(@"\bDG\.Tweening|\bDOTween\w*|\.DO[A-Z]\w*\s*\(|\b(?:Tweener|TweenCallback|LoopType)\b|\bEase\.\w+", "DOTween*") },
            { "cinemachine", new Rule(@"\bCinemachine\w*|Unity\.Cinemachine", "Unity.Cinemachine*") },

            // ---- Social
            { "facebook", new Rule(@"Facebook\.Unity|\bFB\s*\.\s*\w|\bFacebookSDK\b") },
            { "native-share", new Rule(@"\bNativeShare\b", "NativeShare*") },

            // ---- Core libraries
            { "unitask", new Rule(@"Cysharp\.Threading\.Tasks|\bUniTask\w*|\.Forget\s*\(", "UniTask*") },
            { "newtonsoft", new Rule(@"Newtonsoft\.Json|\b(?:JsonConvert|JObject|JArray|JToken|JsonProperty|JsonIgnore)\b", "Unity.Newtonsoft.Json*") },
            { "addressables", new Rule(@"UnityEngine\.AddressableAssets|UnityEngine\.ResourceManagement|\bAddressables\b|\bAssetReference\w*|\bAsyncOperationHandle\b", "Unity.Addressables*", "Unity.ResourceManager*") },
            { "localization", new Rule(@"UnityEngine\.Localization|\b(?:LocalizedString|LocalizationSettings|LocalizedAsset\w*)\b", "Unity.Localization*") },
            { "input-system", new Rule(@"UnityEngine\.InputSystem|\b(?:InputAction\w*|PlayerInput|InputSystem|Touchscreen)\b|\b(?:Keyboard|Mouse|Gamepad)\.current\b", "Unity.InputSystem*") },

            // ---- Platform & Tools
            { "edm4u", new Rule(@"Google\.JarResolver|Google\.IOSResolver|\bGooglePlayServices\b|\bPlayServicesResolver\b") },
            { "play-games", new Rule(@"GooglePlayGames|\bPlayGamesPlatform\b", "GooglePlayGames*") },
            { "mobile-notifications", new Rule(@"Unity\.Notifications|\b(?:AndroidNotification\w*|iOSNotification\w*)\b", "Unity.Notifications*") },
            { "remote-config", new Rule(@"Unity\.Services\.RemoteConfig|\bRemoteConfigService\b", "Unity.Services.RemoteConfig*") },
        };

        public static bool TryGet(string id, out Rule rule) { return rules.TryGetValue(id, out rule); }
    }
}
