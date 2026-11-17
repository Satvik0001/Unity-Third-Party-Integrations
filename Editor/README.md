# 3rd Party Integrations Manager for Unity

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-iOS%20%7C%20Android%20%7C%20Standalone-lightgrey.svg)]()

A Unity Editor tool that lets you **add, inspect, and remove common mobile game SDKs and packages** (AdMob, AppLovin MAX, LevelPlay, Firebase, AppsFlyer, Unity IAP, DOTween, and 20+ more) through a clean, categorized checkbox window.

When removing an SDK, it features **Smart Safe Code Cleanup** that comments out SDK-specific calls and dead `.asmdef` references so your project continues to compile without breaking scene transitions (`SceneManager.LoadScene`), level progression, or gameplay logic. Every modification is backed up automatically with a 1-click restore option.

---

## Features

- **Categorized Dashboard:** View all installed vs. available SDKs side-by-side with search and category filtering (**Tools → 3rd Party Integrations**).
- **Multiple Installation Types Handled Automatically:**
  - **Unity Registry Packages:** Installed via Unity Package Manager (`Client.AddAndRemove`).
  - **Git Packages:** Cloned and added via Package Manager git URLs.
  - **GitHub Releases:** Directly downloads the latest `.unitypackage` asset from official GitHub releases and imports it into your project.
  - **Manual Packages:** Prompts to select vendor-provided `.unitypackage` files and caches selections.
- **Smart Code Cleaner (Safe & Non-Destructive):**
  - Comments out code lines referencing deleted SDKs (with `// [TPI removed:<sdk>]` markers).
  - **Protects Gameplay & Scenes:** Preserves `SceneManager.LoadScene`, `Time.timeScale`, audio, animations, and non-ad code.
  - **Preserves Method Declarations:** Wrapper methods (e.g. `ShowInterstitialAds()`) remain intact so caller scripts across other files never throw missing-method compilation errors.
  - **Automatic Fallbacks:** Methods returning status (e.g. `IsInterstitialReady()`) receive safe `return false;` fallbacks when ad references are removed.
  - Cleans up dead assembly definition (`.asmdef`) references.
  - Braces and parentheses balance verification prevents broken syntax.
- **1-Click Restore:** Backups are stored in `Library/TPI_Backup/` before touching any code. Restore anytime via **Tools → Restore Last 3rd Party Code Cleanup**.

---

## Supported Integrations

| Category | Integrations Included | Install Method |
| :--- | :--- | :--- |
| **Ads & Mediation** | Google AdMob, AppLovin MAX, Unity LevelPlay (ironSource), TopOn (AnyThink) | GitHub Release / Unity UPM / Manual |
| **Privacy & Consent** | Apple App Tracking Transparency (ATT / iOS Support) | Unity Package |
| **Analytics & Attribution** | Firebase (Analytics, Crashlytics, Remote Config), AppsFlyer, Adjust, Unity Analytics | Manual / GitHub Release / Unity UPM |
| **In-App Purchases** | Unity IAP (Google Play / App Store) | Unity Package |
| **Vibration & Haptics** | CandyCoded Haptic Feedback | Git Package |
| **Game Feel & Polish** | Feel (More Mountains), DOTween, Cinemachine | Manual / Unity Package |
| **Social & Sharing** | Facebook SDK, Native Share | Manual / Git Package |
| **Core Libraries** | UniTask, Newtonsoft JSON, Addressables, Localization, New Input System | Git Package / Unity Package |
| **Platform & Tools** | External Dependency Manager (EDM4U), Google Play Games, Mobile Notifications, Remote Config, Android Logcat | GitHub Release / Unity Package |

---

## Installation

### Option 1: Via Unity Package Manager (Recommended)
1. In Unity, open **Window → Package Manager**.
2. Click the **`+`** icon in the top left and select **Add package from git URL...**.
3. Enter:
   ```text
   https://github.com/Satvik0001/Unity-Third-Party-Integrations.git](https://github.com/Satvik0001/Unity-Third-Party-Integrations.git?path=/Editor
   ```
4. Click **Add**.

### Option 2: Manual Installation
1. Clone or download this repository.
2. Copy the `Editor` folder into your project's `Assets/` directory (e.g. `Assets/Plugins/TPIntegrations/Editor/` or `Assets/Editor/`).

---

## How to Use

### 1. Opening the Manager
Go to the top menu bar in Unity:
```text
Tools → 3rd Party Integrations
```

### 2. Adding Integrations
1. Select one or more integrations from the **NOT INSTALLED** column on the right.
2. Filter by category or search by name using the toolbar.
3. Click **Add Selected**.
4. The tool will download and import GitHub release packages, prompt for any manual packages, and send UPM packages to the Package Manager in one batch.

### 3. Removing Integrations & Safe Code Cleanup
1. Select installed integrations from the **INSTALLED** column on the left.
2. Click **Remove Selected**.
3. You will see a summary preview of folders to be deleted and scripts to be cleaned.
4. Choose **Remove + comment out code**.
5. Dead SDK code and `.asmdef` references are safely commented out while keeping your scene loading, level progression, and gameplay logic intact.

### 4. Restoring Code
If you ever want to revert the latest code cleanup:
```text
Tools → Restore Last 3rd Party Code Cleanup
```
Select **Restore** to roll back your scripts to their pre-cleanup state.

---

## License

This project is licensed under the [MIT License](LICENSE).
