# 3rd Party Integrations Manager for Unity

Add and remove the SDKs mobile games usually need (ads, attribution, haptics, IAP, core libraries...) from **one Editor window**, with checkboxes and Add / Remove buttons. No hunting for download pages, no digging through `manifest.json`.

![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black) ![License MIT](https://img.shields.io/badge/license-MIT-blue)

## Features

- **Two panels:** *Installed* and *Not installed*, so you always see what your project has.
- **Category dropdowns** (Ads & Mediation, Analytics & Attribution, Vibration & Haptics, ...) with a select-all checkbox per category.
- **One-click Add / Remove** for many SDKs, with a confirmation that lists exactly what will change.
- **Search** and a **category filter** in the toolbar.
- **Safe removal:** shows which folders will be deleted and warns about shared dependencies such as EDM4U.
- **Easy to extend:** every SDK is one line in `IntegrationCatalog.cs`.

## Requirements

| Requirement | Why |
|---|---|
| Unity **2021.3** or newer | Uses `Client.AddAndRemove` and `UnityWebRequest.Result` |
| **Git** installed and on your PATH | Only for installing this tool via git URL, and for "Git pkg" SDKs |
| Internet access to `api.github.com` and `github.com` | Used to download SDKs published as GitHub releases |

## Installation

### Option A: Package Manager (recommended)

1. In Unity open **Window → Package Manager**.
2. Click **+ → Add package from git URL...**
3. Paste:

   ```
   https://github.com/YOUR-USERNAME/YOUR-REPO.git
   ```

4. Click **Add**.

To pin a version, add a tag:

```
https://github.com/YOUR-USERNAME/YOUR-REPO.git#1.0.0
```

### Option B: edit `Packages/manifest.json`

Add this line inside `"dependencies"`:

```json
"com.yourname.tp-integrations": "https://github.com/YOUR-USERNAME/YOUR-REPO.git#1.0.0"
```

### Option C: `.unitypackage` (no Git needed)

1. Download `ThirdPartyIntegrations.unitypackage` from the [Releases](https://github.com/YOUR-USERNAME/YOUR-REPO/releases) page.
2. In Unity choose **Assets → Import Package → Custom Package...** and import everything.

## Usage

1. Open **Tools → 3rd Party Integrations**.
2. Pick a category in the toolbar, or expand a category in either panel.
3. Tick the SDKs you want.
4. Click **Add Selected** (right panel) or **Remove Selected** (left panel) and confirm.

Unity recompiles after an install; the window updates by itself.

## How each SDK is installed

| Method | What happens | Examples |
|---|---|---|
| **Unity pkg** | Installed by the Package Manager from Unity's registry | ATT, Unity IAP, LevelPlay, Cinemachine, Addressables |
| **Git pkg** | Installed by the Package Manager from a git URL (needs Git) | Haptic Feedback, Native Share, UniTask |
| **GitHub** | The tool downloads the latest `.unitypackage` from the repo's GitHub release and imports it | AdMob, AppLovin MAX, AppsFlyer, Adjust, EDM4U, Google Play Games |
| **Manual** | Needs a login or the Asset Store, so you download the file and pick it once | Feel, DOTween, TopOn, Firebase, Facebook SDK |

Downloads are cached in `Library/TPI_Cache` (clear them with **Tools → Clear 3rd Party Download Cache**).

## Included SDKs

| Category | SDKs |
|---|---|
| Ads & Mediation | AdMob, AppLovin MAX, Unity LevelPlay, TopOn (AnyThink) |
| Privacy & Consent | ATT (App Tracking Transparency) |
| Analytics & Attribution | Firebase, AppsFlyer, Adjust, Unity Analytics |
| In-App Purchases | Unity IAP |
| Vibration & Haptics | Haptic Feedback (CandyCoded); Nice Vibrations ships inside Feel |
| Game Feel & Polish | Feel, DOTween, Cinemachine |
| Social & Sharing | Facebook SDK, Native Share |
| Core Libraries | UniTask, Newtonsoft Json, Addressables, Localization, Input System |
| Platform & Tools | EDM4U, Google Play Games Services, Mobile Notifications, Remote Config, Android Logcat |

## Add your own SDK

Open `Editor/IntegrationCatalog.cs` and add one line to the `All` list. Pick the helper that matches how the SDK is distributed:

```csharp
// Unity registry package
Unity("my-id", "My SDK", Tools, "Short description.", "com.unity.my-package"),

// UPM package from a git URL
Git("my-id", "My SDK", Tools, "Short description.",
    "com.company.mysdk", "https://github.com/company/mysdk.git#v1.2.3"),

// .unitypackage attached to a GitHub release (downloaded automatically)
GitHub("my-id", "My SDK", Tools, "Short description.",
    "owner/repo", new[] { "Assets/MySdk" }),

// Needs a manual download (Asset Store, vendor dashboard)
Manual("my-id", "My SDK", Tools, "Short description.",
    "https://vendor.example/download", new[] { "Assets/MySdk" }),
```

Rules:

- `id` must be unique.
- For GitHub and Manual SDKs, the folder list is used both to **detect** the SDK and to **delete** it on Remove, so list only folders that belong to that SDK.
- For a new category, add a constant and put it in the `Categories` array.

## Troubleshooting

| Problem | Fix |
|---|---|
| `Cannot connect to ... (ETIMEDOUT)` from the Package Manager | The Package Manager has its own network stack. Prefer the **GitHub** or **Manual** SDKs, or check firewall / proxy / IPv6 settings for Unity. |
| Git packages fail with `git` errors | Install Git and make sure `git --version` works in a terminal, then restart Unity Hub and the Editor. |
| `GitHub API ... HTTP 403` | GitHub's anonymous rate limit was hit. Wait a few minutes and try again. |
| "The latest release has no .unitypackage file" | The vendor changed how they publish. Use the **Page** button, download manually, and import via **Assets → Import Package**. |
| An SDK imported but still shows under *Not installed* | The folder paths in the catalog entry don't match where that SDK installs. Fix the `Paths` list. |
| Compile errors after removing an SDK | Your scripts still reference it. Remove or comment out those usages. |
| Compile errors after installing this tool | Make sure the scripts live in an `Editor` folder (they do when installed as a package) and that no older copy of the tool is still in `Assets/`. |

## Contributing

Pull requests that add or fix catalog entries are welcome.

1. Fork the repo and create a branch.
2. Add or edit entries in `Editor/IntegrationCatalog.cs`.
3. Test **Add** and **Remove** in a clean Unity project.
4. Mention the SDK's install source (docs link) in the PR description.

## Disclaimer

SDK names and logos belong to their owners. This tool only downloads or installs packages from the vendors' own sources. Always review each SDK's license and privacy requirements before shipping.

## License

MIT. See [LICENSE](LICENSE).
