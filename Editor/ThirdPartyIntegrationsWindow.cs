using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace TPIntegrations.Editor
{
    public class ThirdPartyIntegrationsWindow : EditorWindow
    {
        // ---- persistent UI state
        readonly HashSet<string> selected = new HashSet<string>();
        readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
        readonly Dictionary<string, bool> installedState = new Dictionary<string, bool>();
        Vector2 scrollInstalled, scrollAvailable;
        string search = "";
        int categoryFilter;
        GUIStyle wrapMini;

        // ---- work state (changed by callbacks, copied into cached* at Layout time)
        string status = "";
        MessageType statusType = MessageType.None;
        string progressText = "";
        bool needsRefresh;
        AddAndRemoveRequest request;
        Routine routine;

        // ---- values used for drawing (keeps Layout and Repaint identical)
        bool cachedBusy, cachedCanCancel;
        string cachedStatus = "", cachedProgress = "";
        MessageType cachedStatusType = MessageType.None;

        bool Busy { get { return routine != null || request != null; } }

        [MenuItem("Tools/3rd Party Integrations")]
        public static void ShowWindow()
        {
            var w = GetWindow<ThirdPartyIntegrationsWindow>("3rd Party Integrations");
            w.minSize = new Vector2(780, 460);
        }

        [MenuItem("Tools/Clear 3rd Party Download Cache")]
        static void ClearCache()
        {
            if (Directory.Exists(Installer.CacheDir)) Directory.Delete(Installer.CacheDir, true);
            Debug.Log("[TPI] Download cache cleared.");
        }

        // ------------------------------------------------------------------ lifecycle
        void OnEnable()
        {
            Refresh();
            UnityEditor.PackageManager.Events.registeredPackages += OnPackagesRegistered;
            AssetDatabase.importPackageCompleted += OnImportCompleted;
        }

        void OnDisable()
        {
            UnityEditor.PackageManager.Events.registeredPackages -= OnPackagesRegistered;
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            EditorApplication.update -= PollRequest;
            if (routine != null) routine.Stop();
            routine = null;
        }

        void OnFocus() { needsRefresh = true; }

        void OnPackagesRegistered(PackageRegistrationEventArgs args) { needsRefresh = true; Repaint(); }

        void OnImportCompleted(string packageName) { needsRefresh = true; Repaint(); }

        // ------------------------------------------------------------------ state
        void Refresh()
        {
            var deps = ManifestEditor.ReadDependencies();
            installedState.Clear();
            foreach (var i in IntegrationCatalog.All)
            {
                bool installed = IsPackage(i)
                    ? deps.Contains(i.PackageName)
                    : i.Paths != null && i.Paths.Any(PathExists);
                installedState[i.Id] = installed;
            }
        }

        static string Abs(string projectRelative)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectRelative));
        }

        static bool PathExists(string p) { return Directory.Exists(Abs(p)) || File.Exists(Abs(p)); }

        // Package Manager based kinds (Unity registry or git URL)
        static bool IsPackage(Integration i)
        {
            return i.Kind == InstallKind.UnityPackage || i.Kind == InstallKind.GitPackage;
        }

        // what Client.AddAndRemove needs to add the package
        static string AddArgument(Integration i)
        {
            return i.Kind == InstallKind.GitPackage ? i.GitUrl : i.PackageName;
        }

        bool Matches(Integration i)
        {
            if (categoryFilter > 0 && i.Category != IntegrationCatalog.Categories[categoryFilter - 1]) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;
            string s = search.Trim();
            return i.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0
                || i.Description.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0
                || i.Category.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void SetProgress(string text) { progressText = text; Repaint(); }

        // ------------------------------------------------------------------ GUI
        void OnGUI()
        {
            if (wrapMini == null) wrapMini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

            // Only change what is drawn during the Layout event, so Layout and Repaint always match.
            if (Event.current.type == EventType.Layout)
            {
                if (needsRefresh) { Refresh(); needsRefresh = false; }
                cachedBusy = Busy;
                cachedCanCancel = routine != null;
                cachedStatus = status;
                cachedStatusType = statusType;
                cachedProgress = progressText;
            }

            DrawToolbar();

            EditorGUI.BeginDisabledGroup(cachedBusy);
            EditorGUILayout.BeginHorizontal();
            DrawPanel(true);
            DrawPanel(false);
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            if (cachedBusy)
            {
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(cachedProgress) ? "Working…" : cachedProgress, MessageType.Info);
                if (cachedCanCancel && GUILayout.Button("Cancel", GUILayout.Width(80)))
                    EditorApplication.delayCall += CancelFlow;
            }
            else if (!string.IsNullOrEmpty(cachedStatus))
            {
                EditorGUILayout.HelpBox(cachedStatus, cachedStatusType);
            }
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) Refresh();
            GUILayout.Space(8);
            GUILayout.Label("Category", EditorStyles.miniLabel, GUILayout.Width(52));
            var cats = new[] { "All" }.Concat(IntegrationCatalog.Categories).ToArray();
            categoryFilter = Mathf.Clamp(categoryFilter, 0, cats.Length - 1);
            categoryFilter = EditorGUILayout.Popup(categoryFilter, cats, EditorStyles.toolbarPopup, GUILayout.Width(180));
            GUILayout.FlexibleSpace();
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
        }

        void DrawPanel(bool installedPanel)
        {
            var items = IntegrationCatalog.All
                .Where(i => installedState.ContainsKey(i.Id) && installedState[i.Id] == installedPanel && Matches(i))
                .ToList();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.5f - 6));
            GUILayout.Label((installedPanel ? "INSTALLED" : "NOT INSTALLED") + "  (" + items.Count + ")", EditorStyles.boldLabel);

            Vector2 scroll = installedPanel ? scrollInstalled : scrollAvailable;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            bool forceOpen = !string.IsNullOrWhiteSpace(search);
            foreach (var cat in IntegrationCatalog.Categories)
            {
                var group = items.Where(i => i.Category == cat).ToList();
                if (group.Count == 0) continue;

                string key = (installedPanel ? "I:" : "N:") + cat;
                bool isOpen;
                foldouts.TryGetValue(key, out isOpen);
                bool showItems = isOpen || forceOpen;

                EditorGUILayout.BeginHorizontal();
                bool all = group.All(g => selected.Contains(g.Id));
                bool any = group.Any(g => selected.Contains(g.Id));
                EditorGUI.showMixedValue = any && !all;
                bool newAll = EditorGUILayout.Toggle(all, GUILayout.Width(16));
                EditorGUI.showMixedValue = false;
                if (newAll != all)
                {
                    foreach (var g in group)
                    {
                        if (newAll) selected.Add(g.Id); else selected.Remove(g.Id);
                    }
                }

                bool open = EditorGUILayout.Foldout(showItems, cat + "  (" + group.Count + ")", true);
                if (!forceOpen) foldouts[key] = open;
                EditorGUILayout.EndHorizontal();

                if (showItems)
                    foreach (var item in group) DrawItem(item);

                GUILayout.Space(4);
            }

            if (items.Count == 0)
                GUILayout.Label(installedPanel ? "Nothing installed from the catalog." : "Everything in the catalog is installed.",
                    EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndScrollView();
            if (installedPanel) scrollInstalled = scroll; else scrollAvailable = scroll;

            // ---- bottom buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(80))) foreach (var i in items) selected.Add(i.Id);
            if (GUILayout.Button("Clear", GUILayout.Width(60))) foreach (var i in items) selected.Remove(i.Id);

            var picked = items.Where(i => selected.Contains(i.Id)).ToList();
            EditorGUI.BeginDisabledGroup(picked.Count == 0);
            string label = installedPanel ? "Remove Selected (" + picked.Count + ")" : "Add Selected (" + picked.Count + ")";
            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                // run after the GUI pass so dialogs do not break IMGUI layout
                if (installedPanel) EditorApplication.delayCall += () => RemoveIntegrations(picked);
                else EditorApplication.delayCall += () => AddIntegrations(picked);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void DrawItem(Integration i)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18);
            bool on = selected.Contains(i.Id);
            bool now = EditorGUILayout.ToggleLeft(i.Name, on, EditorStyles.boldLabel);
            if (now != on) { if (now) selected.Add(i.Id); else selected.Remove(i.Id); }
            GUILayout.Label(KindLabel(i), EditorStyles.miniLabel, GUILayout.Width(60));
            if (!string.IsNullOrEmpty(i.Url) && GUILayout.Button("Page", EditorStyles.miniButton, GUILayout.Width(38)))
                Application.OpenURL(i.Url);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(36);
            GUILayout.Label(i.Description, wrapMini);
            EditorGUILayout.EndHorizontal();
        }

        static string KindLabel(Integration i)
        {
            switch (i.Kind)
            {
                case InstallKind.UnityPackage: return "Unity pkg";
                case InstallKind.GitPackage: return "Git pkg";
                case InstallKind.GitHubRelease: return "GitHub";
                default: return "Manual";
            }
        }

        // ------------------------------------------------------------------ ADD
        void AddIntegrations(List<Integration> list)
        {
            if (Busy) return;

            string msg = "Add these integrations?\n\n" + string.Join("\n", list.Select(i => "• " + i.Name).ToArray());
            if (list.Any(i => i.Kind == InstallKind.GitHubRelease))
                msg += "\n\nGitHub SDKs are downloaded automatically (latest release) and imported.";
            if (list.Any(i => i.Kind == InstallKind.GitPackage))
                msg += "\n\nGit packages are installed by the Package Manager and need Git installed on this computer.";
            if (list.Any(i => i.Kind == InstallKind.Manual))
                msg += "\n\nManual SDKs will ask you to pick the .unitypackage you downloaded.";
            if (!EditorUtility.DisplayDialog("Add integrations", msg, "Add", "Cancel")) return;

            status = "";
            statusType = MessageType.None;
            progressText = "Starting…";
            routine = Routine.Start(AddFlow(list), OnFlowEnded);
            Repaint();
        }

        IEnumerator AddFlow(List<Integration> list)
        {
            var report = new List<string>();
            var succeeded = new List<string>();
            bool anyError = false;

            // 1) download GitHub releases (async, no domain reload yet)
            var downloads = new List<ReleaseResult>();
            foreach (var i in list.Where(x => x.Kind == InstallKind.GitHubRelease).ToList())
            {
                var res = new ReleaseResult();
                var inner = Installer.FetchRelease(i, res, SetProgress);
                while (inner.MoveNext()) yield return null;
                downloads.Add(res);
            }

            // 2) collect every package file to import
            progressText = "Importing…";
            var toImport = new List<string>();

            foreach (var d in downloads)
            {
                if (!string.IsNullOrEmpty(d.Error))
                {
                    anyError = true;
                    report.Add("✗ " + d.Item.Name + ": " + d.Error + "  (manual download: " + d.Item.Url + ")");
                }
                else
                {
                    toImport.Add(d.File);
                    succeeded.Add(d.Item.Id);
                    report.Add("✓ " + d.Item.Name + " ← " + d.AssetName + " (" + d.Tag + ")");
                }
            }

            foreach (var i in list.Where(x => x.Kind == InstallKind.Manual).ToList())
            {
                var files = PickManualFiles(i);
                if (files.Count == 0) { report.Add("– " + i.Name + ": skipped"); continue; }
                toImport.AddRange(files);
                succeeded.Add(i.Id);
                report.Add("✓ " + i.Name + " ← " + string.Join(", ", files.Select(f => Path.GetFileName(f)).ToArray()));
            }

            // 3) import (silent) - Unity recompiles once afterwards
            foreach (var f in toImport) AssetDatabase.ImportPackage(f, false);

            // 4) Unity registry packages in ONE Package Manager request
            var upm = list.Where(x => IsPackage(x)).ToList();
            foreach (var u in upm) report.Add("• " + u.Name + ": sent to Package Manager");

            foreach (var id in succeeded) selected.Remove(id);
            foreach (var u in upm) selected.Remove(u.Id);

            status = string.Join("\n", report.ToArray());
            statusType = anyError ? MessageType.Warning : MessageType.Info;
            foreach (var line in report) Debug.Log("[TPI] " + line);

            if (upm.Count > 0) StartBatch(upm.Select(u => AddArgument(u)).ToArray(), null);
        }

        List<string> PickManualFiles(Integration i)
        {
            var files = new List<string>();
            string dirKey = "TPI_LastDir_" + i.Id;
            string fileKey = "TPI_File_" + i.Id;

            if (!i.MultiPackage)
            {
                string saved = EditorPrefs.GetString(fileKey, "");
                if (!string.IsNullOrEmpty(saved) && File.Exists(saved) &&
                    EditorUtility.DisplayDialog(i.Name, "Use the package you picked last time?\n\n" + saved, "Use it", "Pick another"))
                {
                    files.Add(saved);
                    return files;
                }
            }

            while (true)
            {
                int choice = EditorUtility.DisplayDialogComplex(i.Name,
                    i.Name + " is installed from a .unitypackage.\n\nDownload it first (vendor / Asset Store page), then pick the file.",
                    "Pick .unitypackage…", "Open download page", "Skip");

                if (choice == 1)
                {
                    if (!string.IsNullOrEmpty(i.Url)) Application.OpenURL(i.Url);
                    break;
                }
                if (choice != 0) break;

                string path = EditorUtility.OpenFilePanel("Select " + i.Name + " .unitypackage",
                    EditorPrefs.GetString(dirKey, ""), "unitypackage");
                if (string.IsNullOrEmpty(path)) break;

                EditorPrefs.SetString(dirKey, Path.GetDirectoryName(path));
                EditorPrefs.SetString(fileKey, path);
                files.Add(path);

                if (!i.MultiPackage) break;
                if (!EditorUtility.DisplayDialog(i.Name, "Imported list so far: " + files.Count + ".\n\nPick another .unitypackage for " + i.Name + "?", "Pick another", "Done"))
                    break;
            }
            return files;
        }

        void OnFlowEnded(Exception e)
        {
            routine = null;
            if (e != null)
            {
                status = "Unexpected error: " + e.Message;
                statusType = MessageType.Error;
                Debug.LogException(e);
            }
            needsRefresh = true;
            Repaint();
        }

        void CancelFlow()
        {
            if (routine == null) return;
            routine.Stop();
            routine = null;
            progressText = "";
            status = "Cancelled.";
            statusType = MessageType.None;
            Repaint();
        }

        // ------------------------------------------------------------------ REMOVE
        void RemoveIntegrations(List<Integration> list)
        {
            if (Busy) return;

            string msg = "Remove these integrations?\n\n" + string.Join("\n", list.Select(i => "• " + i.Name).ToArray());

            var folderItems = list.Where(i => !IsPackage(i)).ToList();
            if (folderItems.Count > 0)
            {
                var existing = folderItems.SelectMany(i => i.Paths).Where(PathExists).Distinct().ToList();
                msg += "\n\nThese folders / files will be DELETED:\n" + string.Join("\n", existing.Select(p => "  " + p).ToArray());
            }
            foreach (var i in list.Where(x => !string.IsNullOrEmpty(x.RemoveWarning)))
                msg += "\n\n⚠ " + i.Name + ": " + i.RemoveWarning;
            msg += "\n\nScripts that reference these SDKs may stop compiling. Make sure your work is committed.";

            if (!EditorUtility.DisplayDialog("Remove integrations", msg, "Remove", "Cancel")) return;

            var report = new List<string>();
            foreach (var i in folderItems)
            {
                foreach (var p in i.Paths.Where(PathExists))
                {
                    if (!AssetDatabase.DeleteAsset(p))
                        report.Add("✗ Could not delete " + p);
                }
                report.Add("✓ " + i.Name + " removed");
            }

            var upm = list.Where(i => IsPackage(i)).ToList();
            foreach (var i in list) selected.Remove(i.Id);

            status = string.Join("\n", report.ToArray());
            statusType = report.Any(r => r.StartsWith("✗")) ? MessageType.Warning : MessageType.Info;

            AssetDatabase.Refresh();
            if (upm.Count > 0) StartBatch(null, upm.Select(i => i.PackageName).ToArray());
            else { Refresh(); Repaint(); }
        }

        // ------------------------------------------------------------------ Package Manager request
        void StartBatch(string[] add, string[] remove)
        {
            progressText = "Package Manager is working…";
            request = Client.AddAndRemove(add, remove);
            EditorApplication.update -= PollRequest;
            EditorApplication.update += PollRequest;
            Repaint();
        }

        void PollRequest()
        {
            if (request == null) { EditorApplication.update -= PollRequest; return; }
            if (!request.IsCompleted) return;

            EditorApplication.update -= PollRequest;
            string prefix = string.IsNullOrEmpty(status) ? "" : status + "\n";
            if (request.Status == StatusCode.Failure)
            {
                status = prefix + "✗ Package Manager error: " + request.Error.message;
                statusType = MessageType.Error;
                Debug.LogError("[TPI] " + request.Error.message);
            }
            else
            {
                status = prefix + "✓ Unity packages updated.";
            }

            request = null;
            needsRefresh = true;
            Repaint();
        }
    }
}
