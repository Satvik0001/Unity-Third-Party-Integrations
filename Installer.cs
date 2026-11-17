using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace TPIntegrations.Editor
{
    public sealed class ReleaseResult
    {
        public Integration Item;
        public string File;       // local path of the downloaded .unitypackage
        public string AssetName;  // file name picked from the release
        public string Tag;        // release tag
        public string Error;      // null when successful
    }

    /// <summary>Runs an IEnumerator from EditorApplication.update (one MoveNext per editor tick).</summary>
    public sealed class Routine
    {
        readonly IEnumerator it;
        readonly Action<Exception> onEnd;
        bool done;

        Routine(IEnumerator it, Action<Exception> onEnd) { this.it = it; this.onEnd = onEnd; }

        public static Routine Start(IEnumerator it, Action<Exception> onEnd)
        {
            var r = new Routine(it, onEnd);
            EditorApplication.update += r.Tick;
            return r;
        }

        public void Stop()
        {
            if (done) return;
            done = true;
            EditorApplication.update -= Tick;
        }

        void Tick()
        {
            if (done) return;
            Exception err = null;
            bool finished = false;
            try { if (!it.MoveNext()) finished = true; }
            catch (Exception e) { err = e; finished = true; }

            if (finished)
            {
                Stop();
                if (onEnd != null) onEnd(err);
            }
        }
    }

    public static class Installer
    {
        public static string CacheDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "TPI_Cache"));

        /// <summary>Finds the latest GitHub release of the integration and downloads its .unitypackage into Library/TPI_Cache.</summary>
        public static IEnumerator FetchRelease(Integration i, ReleaseResult res, Action<string> progress)
        {
            res.Item = i;
            string json;

            if (progress != null) progress("Checking latest " + i.Name + " release…");
            using (var req = UnityWebRequest.Get("https://api.github.com/repos/" + i.Repo + "/releases/latest"))
            {
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.timeout = 30;
                var op = req.SendWebRequest();
                while (!op.isDone) yield return null;

                if (req.result != UnityWebRequest.Result.Success)
                {
                    string hint = req.responseCode == 403 ? " (GitHub rate limit - try again in a few minutes)" : "";
                    res.Error = "GitHub API: " + req.error + " [HTTP " + req.responseCode + "]" + hint;
                    yield break;
                }
                json = req.downloadHandler.text;
            }

            string url, name, tag, err;
            if (!PickAsset(json, i, out url, out name, out tag, out err))
            {
                res.Error = err;
                yield break;
            }
            res.AssetName = name;
            res.Tag = tag;

            string dir = Path.Combine(CacheDir, i.Id, Sanitize(tag));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, Sanitize(name));

            if (File.Exists(file) && new FileInfo(file).Length > 0)
            {
                res.File = file; // already downloaded earlier
                yield break;
            }

            string tmp = file + ".part";
            if (File.Exists(tmp)) File.Delete(tmp);

            bool ok;
            string dlError = null;
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerFile(tmp) { removeFileOnAbort = true };
                req.timeout = 900;
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    if (progress != null)
                        progress("Downloading " + name + "…  " + Mathf.RoundToInt(op.progress * 100f) + "%");
                    yield return null;
                }
                ok = req.result == UnityWebRequest.Result.Success;
                if (!ok) dlError = "Download failed: " + req.error + " [HTTP " + req.responseCode + "]";
            }

            if (!ok) { res.Error = dlError; yield break; }

            try
            {
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
                res.File = file;
            }
            catch (Exception e)
            {
                res.Error = "Could not save download: " + e.Message;
            }
        }

        static bool PickAsset(string json, Integration i, out string url, out string name, out string tag, out string error)
        {
            url = null; name = null; tag = null; error = null;
            try
            {
                var root = MiniJson.Parse(json) as Dictionary<string, object>;
                if (root == null) { error = "Unexpected GitHub response."; return false; }

                object tagObj;
                tag = root.TryGetValue("tag_name", out tagObj) && tagObj is string ? (string)tagObj : "latest";

                object assetsObj;
                var assets = root.TryGetValue("assets", out assetsObj) ? assetsObj as List<object> : null;
                if (assets == null || assets.Count == 0)
                {
                    error = "The latest release has no downloadable files.";
                    return false;
                }

                var candidates = new List<KeyValuePair<string, string>>();
                foreach (var o in assets)
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    object n, u;
                    d.TryGetValue("name", out n);
                    d.TryGetValue("browser_download_url", out u);
                    var assetName = n as string;
                    var assetUrl = u as string;
                    if (assetName == null || assetUrl == null) continue;
                    if (!assetName.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase)) continue;
                    if (i.AssetExcludes != null &&
                        i.AssetExcludes.Any(x => assetName.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    candidates.Add(new KeyValuePair<string, string>(assetName, assetUrl));
                }

                if (candidates.Count == 0)
                {
                    error = "The latest release (" + tag + ") has no .unitypackage file.";
                    return false;
                }

                if (!string.IsNullOrEmpty(i.AssetContains))
                {
                    var preferred = candidates
                        .Where(c => c.Key.IndexOf(i.AssetContains, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    if (preferred.Count > 0) candidates = preferred;
                }

                name = candidates[0].Key;
                url = candidates[0].Value;
                return true;
            }
            catch (Exception e)
            {
                error = "Could not read release info: " + e.Message;
                return false;
            }
        }

        static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }
    }
}
