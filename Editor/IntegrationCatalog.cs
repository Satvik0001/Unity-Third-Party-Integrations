using System.Collections.Generic;

namespace TPIntegrations.Editor
{
    public enum InstallKind
    {
        UnityPackage,   // Unity's own registry  -> Package Manager (Client.AddAndRemove)
        GitPackage,     // UPM package from a git URL -> Package Manager (needs Git installed)
        GitHubRelease,  // .unitypackage attached to the latest GitHub release -> downloaded by the Editor itself
        Manual          // Asset Store / vendor dashboard -> you pick the downloaded .unitypackage
    }

    public class Integration
    {
        public string Id, Name, Category, Description;
        public InstallKind Kind;

        public string PackageName;      // UnityPackage / GitPackage: package id in manifest.json
        public string GitUrl;           // GitPackage: git URL (optionally with #tag or ?path=)
        public string Repo;             // GitHubRelease: "owner/repo"
        public string AssetContains;    // GitHubRelease: prefer assets whose name contains this
        public string[] AssetExcludes;  // GitHubRelease: skip assets whose name contains any of these

        public string[] Paths;          // GitHubRelease / Manual: folders used to DETECT and to REMOVE
        public string Url;              // page to open (releases, store, docs)
        public string RemoveWarning;    // extra text shown in the remove confirmation
        public bool MultiPackage;       // Manual: SDK ships several .unitypackage files
    }

    /// <summary>Add your own SDKs here. One entry per SDK.</summary>
    public static class IntegrationCatalog
    {
        const string Ads = "Ads & Mediation";
        const string Privacy = "Privacy & Consent";
        const string Analytics = "Analytics & Attribution";
        const string Iap = "In-App Purchases";
        const string Haptics = "Vibration & Haptics";
        const string Feel = "Game Feel & Polish";
        const string Social = "Social & Sharing";
        const string Core = "Core Libraries";
        const string Tools = "Platform & Tools";

        public static readonly string[] Categories =
            { Ads, Privacy, Analytics, Iap, Haptics, Feel, Social, Core, Tools };

        public static readonly List<Integration> All = new List<Integration>
        {
            // ---------------- Ads & Mediation ----------------
            GitHub("admob", "AdMob (Google Mobile Ads)", Ads,
                "Banner / interstitial / rewarded / app-open ads + Google UMP consent. Bundles EDM4U.",
                "googleads/googleads-mobile-unity",
                new[]
                {
                    "Assets/GoogleMobileAds",
                    "Assets/Plugins/Android/googlemobileads-unity.aar",
                    "Assets/Plugins/Android/GoogleMobileAdsPlugin",
                    "Assets/Plugins/iOS/GADUAdNetworkExtras",
                    "Assets/Plugins/iOS/unity-plugin-library.a"
                }),
            GitHub("applovin", "AppLovin MAX", Ads,
                "Mediation + ads. Add networks afterwards via AppLovin > Integration Manager.",
                "AppLovin/AppLovin-MAX-Unity-Plugin",
                new[] { "Assets/MaxSdk" }),
            Unity("levelplay", "Unity LevelPlay (ironSource)", Ads,
                "Unity's ad mediation SDK.", "com.unity.services.levelplay"),
            Manual("topon", "TopOn (AnyThink)", Ads,
                "Ad mediation. Download the Unity SDK (.unitypackage) from the TopOn dashboard / docs.",
                "https://docs.toponad.com",
                new[] { "Assets/AnyThinkAds" }),

            // ---------------- Privacy & Consent ----------------
            Unity("att", "ATT (App Tracking Transparency)", Privacy,
                "iOS 14+ tracking permission prompt (ATTrackingStatusBinding).", "com.unity.ads.ios-support"),

            // ---------------- Analytics & Attribution ----------------
            Manual("firebase", "Firebase (Analytics, Crashlytics, Remote Config...)", Analytics,
                "Download the Firebase Unity SDK zip, unzip it, then pick the products you need (dotnet4 folder).",
                "https://firebase.google.com/download/unity",
                new[] { "Assets/Firebase", "Assets/Plugins/iOS/Firebase" }, multi: true),
            GitHub("appsflyer", "AppsFlyer", Analytics,
                "Install attribution and marketing analytics.",
                "AppsFlyerSDK/appsflyer-unity-plugin",
                new[] { "Assets/AppsFlyer" },
                excludes: new[] { "strict" }),
            GitHub("adjust", "Adjust", Analytics,
                "Install attribution and fraud prevention.",
                "adjust/unity_sdk",
                new[] { "Assets/Adjust" }),
            Unity("unity-analytics", "Unity Analytics", Analytics,
                "Unity Gaming Services analytics.", "com.unity.services.analytics"),

            // ---------------- In-App Purchases ----------------
            Unity("unity-iap", "Unity IAP", Iap,
                "Cross-store in-app purchasing (Google Play / App Store).", "com.unity.purchasing"),

            // ---------------- Vibration & Haptics ----------------
            Git("haptic-feedback", "Haptic Feedback (CandyCoded)", Haptics,
                "Light / Medium / Heavy haptics on iOS and Android with one line of code. Pinned to v1.0.3. For HD haptics see Feel (includes Nice Vibrations).",
                "xyz.candycoded.hapticfeedback",
                "https://github.com/CandyCoded/HapticFeedback.git#v1.0.3"),

            // ---------------- Game Feel & Polish ----------------
            Manual("feel", "Feel (More Mountains)", Feel,
                "Game-feel toolkit: MMFeedbacks, camera shakes, and Nice Vibrations haptics. Asset Store package.",
                "https://assetstore.unity.com/packages/tools/particles-effects/feel-183370",
                new[] { "Assets/Feel", "Assets/MoreMountains" }),
            Manual("dotween", "DOTween", Feel,
                "Tweening engine. Download from the Asset Store or demigiant.com.",
                "http://dotween.demigiant.com/download.php",
                new[] { "Assets/Plugins/Demigiant" }),
            Unity("cinemachine", "Cinemachine", Feel,
                "Camera system (follow, shake, blends).", "com.unity.cinemachine"),

            // ---------------- Social & Sharing ----------------
            Manual("facebook", "Facebook SDK", Social,
                "Facebook login, sharing and app events. Download the Unity SDK from the Meta developer docs.",
                "https://developers.facebook.com/docs/unity/",
                new[] { "Assets/FacebookSDK" }),
            Git("native-share", "Native Share", Social,
                "Share text / images / files through the native Android and iOS share sheet. Check the repo for its maintenance status.",
                "com.yasirkula.nativeshare",
                "https://github.com/yasirkula/UnityNativeShare.git"),

            // ---------------- Core Libraries ----------------
            Git("unitask", "UniTask", Core,
                "Allocation-free async/await for Unity.",
                "com.cysharp.unitask",
                "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"),
            Unity("newtonsoft", "Newtonsoft Json", Core,
                "JSON serialization (used by many SDKs and save systems).", "com.unity.nuget.newtonsoft-json"),
            Unity("addressables", "Addressables", Core,
                "Asset bundles / remote content loading and smaller builds.", "com.unity.addressables"),
            Unity("localization", "Localization", Core,
                "Multi-language UI and assets.", "com.unity.localization"),
            Unity("input-system", "Input System", Core,
                "New input system (touch, gamepad, keyboard).", "com.unity.inputsystem"),

            // ---------------- Platform & Tools ----------------
            GitHub("edm4u", "External Dependency Manager (EDM4U)", Tools,
                "Resolves native Android / iOS dependencies needed by most ad and analytics SDKs.",
                "googlesamples/unity-jar-resolver",
                new[] { "Assets/ExternalDependencyManager" },
                contains: "external-dependency-manager",
                warning: "AdMob, AppLovin, Firebase and other SDKs rely on EDM4U. Removing it can break their Android/iOS builds."),
            GitHub("play-games", "Google Play Games Services", Tools,
                "Sign-in, leaderboards, achievements, cloud save.",
                "playgameservices/play-games-plugin-for-unity",
                new[] { "Assets/GooglePlayGames" }),
            Unity("mobile-notifications", "Mobile Notifications", Tools,
                "Local notifications for Android and iOS.", "com.unity.mobile.notifications"),
            Unity("remote-config", "Remote Config", Tools,
                "Change game values remotely without an app update.", "com.unity.remote-config"),
            Unity("android-logcat", "Android Logcat", Tools,
                "View device logs inside the Unity Editor while testing on Android.", "com.unity.mobile.android-logcat"),
        };

        // ---------------- helpers ----------------
        static Integration Unity(string id, string name, string cat, string desc, string pkg) => new Integration
        {
            Id = id, Name = name, Category = cat, Description = desc,
            Kind = InstallKind.UnityPackage, PackageName = pkg
        };

        static Integration Git(string id, string name, string cat, string desc, string pkg, string gitUrl) => new Integration
        {
            Id = id, Name = name, Category = cat, Description = desc,
            Kind = InstallKind.GitPackage, PackageName = pkg, GitUrl = gitUrl, Url = PageOf(gitUrl)
        };

        static Integration GitHub(string id, string name, string cat, string desc, string repo, string[] paths,
            string contains = null, string[] excludes = null, string warning = null) => new Integration
        {
            Id = id, Name = name, Category = cat, Description = desc,
            Kind = InstallKind.GitHubRelease, Repo = repo, Paths = paths,
            AssetContains = contains, AssetExcludes = excludes, RemoveWarning = warning,
            Url = "https://github.com/" + repo + "/releases"
        };

        static Integration Manual(string id, string name, string cat, string desc, string url, string[] paths,
            bool multi = false) => new Integration
        {
            Id = id, Name = name, Category = cat, Description = desc,
            Kind = InstallKind.Manual, Url = url, Paths = paths, MultiPackage = multi
        };

        static string PageOf(string gitUrl)
        {
            string u = gitUrl;
            int q = u.IndexOfAny(new[] { '?', '#' });
            if (q >= 0) u = u.Substring(0, q);
            if (u.EndsWith(".git")) u = u.Substring(0, u.Length - 4);
            return u;
        }
    }
}
