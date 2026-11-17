using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TPIntegrations.Editor
{
    public sealed class FileEdit
    {
        public string Path;      // project-relative, forward slashes (Assets/...)
        public string NewText;
        public bool Bom;
        public int Changes;      // commented lines, or removed asmdef references
        public bool IsAsmdef;
    }

    public sealed class CleanupPlan
    {
        public readonly List<FileEdit> Edits = new List<FileEdit>();
        public readonly List<string> Skipped = new List<string>();   // files we refused to touch (needs manual work)
        public int Count { get { return Edits.Count; } }
    }

    /// <summary>
    /// After an SDK is removed: comments out the lines of YOUR scripts that still use it and drops dead
    /// .asmdef references, so the project compiles again. Every touched file is backed up first.
    ///
    /// Safety contract:
    /// - Only comments out actual SDK statements and SDK variables.
    /// - NEVER comments out method declarations (e.g. ShowInterstitialAds, ShowRewardedAds remain callable).
    /// - NEVER comments out scene loading (SceneManager.LoadScene), audio, timeScale, or other gameplay logic.
    /// </summary>
    public static class CodeCleaner
    {
        static string BackupRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "TPI_Backup")); } }
        static string ProjectAbs(string rel) { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", rel)); }

        // ------------------------------------------------------------------ PLAN (read-only)
        public static CleanupPlan Plan(IList<Integration> removed)
        {
            var plan = new CleanupPlan();
            var rx = new List<KeyValuePair<string, Regex>>();
            var asmNames = new List<string>();
            foreach (var i in removed)
            {
                CleanupRules.Rule r;
                if (!CleanupRules.TryGet(i.Id, out r)) continue;
                if (!string.IsNullOrEmpty(r.Pattern))
                    rx.Add(new KeyValuePair<string, Regex>(i.Id, new Regex(r.Pattern, RegexOptions.IgnoreCase)));
                if (r.Asmdefs != null) asmNames.AddRange(r.Asmdefs);
            }
            if (rx.Count == 0 && asmNames.Count == 0) return plan;

            // never touch vendor code (any catalog integration's folders) or this tool itself
            var vendor = IntegrationCatalog.All.Where(i => i.Paths != null).SelectMany(i => i.Paths)
                .Select(p => p.TrimEnd('/')).ToList();

            string assets = Application.dataPath;
            foreach (string file in Directory.GetFiles(assets, "*.*", SearchOption.AllDirectories))
            {
                bool cs = file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                bool asm = file.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase);
                if (!cs && !asm) continue;

                string rel = "Assets/" + file.Substring(assets.Length + 1).Replace('\\', '/');
                if (rel.Contains("~/") || rel.Contains("/.")) continue;
                if (vendor.Any(v => rel.Equals(v, StringComparison.OrdinalIgnoreCase) ||
                                    rel.StartsWith(v + "/", StringComparison.OrdinalIgnoreCase))) continue;

                try
                {
                    string text = File.ReadAllText(file);
                    if (text.Contains("TPIntegrations")) continue; // this tool

                    string result; int count;
                    if (cs)
                    {
                        if (rx.Count == 0) continue;
                        if (!CleanCs(text, rx, out result, out count))
                        {
                            if (count < 0) plan.Skipped.Add(rel);
                            continue;
                        }
                    }
                    else
                    {
                        if (asmNames.Count == 0 || !CleanAsmdef(text, asmNames, out result, out count)) continue;
                    }

                    var bytes = File.ReadAllBytes(file);
                    bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                    plan.Edits.Add(new FileEdit { Path = rel, NewText = result, Bom = bom, Changes = count, IsAsmdef = asm });
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[TPI] Could not scan " + rel + ": " + e.Message);
                }
            }
            return plan;
        }

        public static string Describe(CleanupPlan plan, int maxFiles)
        {
            var sb = new StringBuilder();
            foreach (var e in plan.Edits.Take(maxFiles))
                sb.Append("  ").Append(e.Path).Append(e.IsAsmdef ? "  (" + e.Changes + " asmdef reference)" : "  (" + e.Changes + " line" + (e.Changes == 1 ? "" : "s") + ")").Append('\n');
            if (plan.Edits.Count > maxFiles) sb.Append("  …and ").Append(plan.Edits.Count - maxFiles).Append(" more file(s)\n");
            return sb.ToString().TrimEnd('\n');
        }

        // ------------------------------------------------------------------ APPLY
        public static List<string> Apply(CleanupPlan plan)
        {
            var report = new List<string>();
            if (plan.Count == 0) return report;

            string backup = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            int done = 0, lines = 0;
            foreach (var e in plan.Edits)
            {
                try
                {
                    string abs = ProjectAbs(e.Path);
                    string dest = Path.Combine(backup, e.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(abs, dest, true);
                    File.WriteAllText(abs, e.NewText, new UTF8Encoding(e.Bom));
                    done++; lines += e.Changes;
                }
                catch (Exception ex)
                {
                    report.Add("✗ Could not clean " + e.Path + ": " + ex.Message);
                }
            }
            report.Insert(0, "✓ Commented out " + lines + " line(s) / reference(s) in " + done + " file(s). Backup: Library/TPI_Backup (Tools > Restore Last 3rd Party Code Cleanup)");
            foreach (var s in plan.Skipped)
                report.Add("⚠ " + s + " was NOT changed (commenting would have unbalanced its braces) - fix by hand.");
            return report;
        }

        [MenuItem("Tools/Restore Last 3rd Party Code Cleanup")]
        static void RestoreLast()
        {
            if (!Directory.Exists(BackupRoot)) { EditorUtility.DisplayDialog("Restore", "No backup found.", "OK"); return; }
            var last = Directory.GetDirectories(BackupRoot).OrderBy(d => d).LastOrDefault();
            if (last == null) { EditorUtility.DisplayDialog("Restore", "No backup found.", "OK"); return; }

            var files = Directory.GetFiles(last, "*.*", SearchOption.AllDirectories);
            if (!EditorUtility.DisplayDialog("Restore code cleanup",
                    "Restore " + files.Length + " file(s) from the last cleanup (" + Path.GetFileName(last) + ")?\nYour current versions of those files will be overwritten.",
                    "Restore", "Cancel")) return;

            foreach (var f in files)
            {
                string rel = f.Substring(last.Length + 1).Replace('\\', '/');
                File.Copy(f, ProjectAbs(rel), true);
            }
            AssetDatabase.Refresh();
            Debug.Log("[TPI] Restored " + files.Length + " file(s) from " + last);
        }

        // ------------------------------------------------------------------ .asmdef
        static bool CleanAsmdef(string text, List<string> names, out string result, out int count)
        {
            result = null; count = 0;
            var m = Regex.Match(text, "\"references\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            if (!m.Success) return false;

            var keep = new List<string>();
            foreach (Match e in Regex.Matches(m.Groups[1].Value, "\"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                string entry = e.Groups[1].Value;
                string name = ResolveAsmRef(entry);
                bool drop = name != null && names.Any(p => p.EndsWith("*")
                    ? name.StartsWith(p.Substring(0, p.Length - 1), StringComparison.Ordinal)
                    : name == p);
                if (drop) count++; else keep.Add(entry);
            }
            if (count == 0) return false;

            string inner = keep.Count == 0 ? "" : "\n        " + string.Join(",\n        ", keep.Select(k => "\"" + k + "\"").ToArray()) + "\n    ";
            var g = m.Groups[1];
            result = text.Substring(0, g.Index) + inner + text.Substring(g.Index + g.Length);
            return true;
        }

        // "GUID:abcd..." -> assembly name (needs the asmdef to still exist, which is why Plan runs BEFORE deletion)
        static string ResolveAsmRef(string entry)
        {
            if (!entry.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase)) return entry;
            string path = AssetDatabase.GUIDToAssetPath(entry.Substring(5));
            if (string.IsNullOrEmpty(path)) return null;
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (ta == null) return null;
            var mm = Regex.Match(ta.text, "\"name\"\\s*:\\s*\"([^\"]+)\"");
            return mm.Success ? mm.Groups[1].Value : null;
        }

        // ------------------------------------------------------------------ C#
        static readonly Regex ControlHeader = new Regex(@"^\s*(?:else\s+)?(?:if|for|foreach|while|lock|using)\s*\(.*\)\s*$|^\s*else\s*$");
        static readonly Regex MemberSignature = new Regex(@"(?:public|private|protected|internal|static|virtual|override|abstract|async|partial|sealed)\s+");

        /// <summary>
        /// Code that must NEVER be commented out, even if wrapped inside or near an ad block.
        /// Protects scene transitions, timescale changes, audio, and gameplay flow.
        /// </summary>
        static bool IsProtectedNonAdCode(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            return Regex.IsMatch(line, @"\b(?:SceneManager|LoadScene|LoadSceneAsync|timeScale|AudioSource|Application\.Quit)\b")
                || Regex.IsMatch(line, @"\b(?:onComplete|onClosed|onFinish|onSuccess|callback)\s*\.?\s*Invoke\s*\(");
        }

        /// <summary>
        /// Method declarations (e.g. public void ShowInterstitialAds()) are never commented out,
        /// ensuring callers across all other scripts continue to compile cleanly without missing-method errors.
        /// </summary>
        static bool IsMethodDeclaration(string line)
        {
            return MemberSignature.IsMatch(line) && line.Contains("(") && !line.Contains("=");
        }

        /// <returns>true = changed. count &lt; 0 = skipped because the result would not be balanced.</returns>
        static bool CleanCs(string text, List<KeyValuePair<string, Regex>> rx, out string result, out int count)
        {
            result = null; count = 0;
            string eol = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            int n = lines.Length;

            // code[i] = the line with comments and string contents removed; "" for blank / comment / preprocessor lines
            var code = new string[n];
            bool inBlock = false;
            for (int i = 0; i < n; i++)
            {
                string c = StripCode(lines[i], ref inBlock);
                code[i] = c.TrimStart().StartsWith("#") ? "" : c;
            }

            // quick exit if no lines reference any removed SDK
            bool any = false;
            for (int i = 0; i < n && !any; i++)
                if (code[i].Trim().Length > 0 && rx.Any(r => r.Value.IsMatch(code[i]))) any = true;
            if (!any) return false;

            var tag    = new string[n];   // id written in the comment marker; null = line untouched
            var inject = new string[n];   // optional code line to insert after line i (e.g. ";" or fallback return)
            var claimed = new bool[n];

            for (int i = 0; i < n; i++)
            {
                if (claimed[i] || code[i].Trim().Length == 0) continue;

                // Never comment out a method header or protected gameplay code (scene loading, etc.)
                if (IsMethodDeclaration(code[i]) || IsProtectedNonAdCode(code[i])) continue;

                string id = null;
                foreach (var r in rx) if (r.Value.IsMatch(code[i])) { id = r.Key; break; }
                if (id == null) continue;

                // ---- walk back over a statement that started on earlier lines (multi-line call, fluent chain)
                int start = i;
                while (true)
                {
                    int p = PrevCode(code, start);
                    if (p < 0 || claimed[p]) break;
                    if (IsMethodDeclaration(code[p]) || IsProtectedNonAdCode(code[p])) break;
                    bool cont = EndsWithContinuation(code[p]) || StartsWithDot(code[start]);
                    if (!cont) break;
                    start = p;
                }

                // `if (x)` / `else` directly above a body that we are removing -> keep the body valid with ";"
                bool needSemi = false;
                {
                    int p = PrevCode(code, start);
                    if (p >= 0 && !claimed[p] && ControlHeader.IsMatch(code[p]) && !code[start].TrimStart().StartsWith("{"))
                        needSemi = true;
                }

                // ---- walk forward to the end of the statement / block
                int end = i;
                int depth = Net(code[start], '{', '}') + Net(code[start], '(', ')') + Net(code[start], '[', ']');
                for (int k = start + 1; k <= end; k++)
                    depth += Net(code[k], '{', '}') + Net(code[k], '(', ')') + Net(code[k], '[', ']');

                while (true)
                {
                    while (depth > 0 && end + 1 < n)
                    {
                        end++;
                        depth += Net(code[end], '{', '}') + Net(code[end], '(', ')') + Net(code[end], '[', ']');
                    }

                    int nx = NextCode(code, end);
                    if (nx < 0) break;
                    string t = code[nx].TrimStart();
                    string last = code[end].TrimEnd();
                    bool endsStatement = last.EndsWith(";") || last.EndsWith("}");
                    bool header = ControlHeader.IsMatch(code[end]);

                    bool extend =
                        (StartsWithDot(t)) ||                                   // fluent chain
                        (!endsStatement && t.StartsWith("{")) ||                // body on the next line
                        (header && !endsStatement) ||                           // `if (..)` + single statement
                        (endsStatement && Regex.IsMatch(t, @"^else\b"));        // `else` belonging to a removed `if`
                    if (!extend) break;

                    end = nx;
                    depth += Net(code[end], '{', '}') + Net(code[end], '(', ')') + Net(code[end], '[', ']');
                }

                bool hasReturn = false;
                for (int k = start; k <= end; k++)
                {
                    // CRITICAL GUARD: Never comment out method declarations or protected non-ad code
                    if (IsMethodDeclaration(code[k]) || IsProtectedNonAdCode(code[k])) continue;

                    claimed[k] = true;
                    if (code[k].Trim().Length > 0 && tag[k] == null)
                    {
                        tag[k] = id;
                        count++;
                        if (code[k].TrimStart().StartsWith("return ") && code[k].TrimEnd().EndsWith(";"))
                            hasReturn = true;
                    }
                }

                if (needSemi)
                {
                    inject[end] = "; // [TPI] keeps the if/else body valid";
                }
                else if (hasReturn)
                {
                    inject[end] = "return false; // [TPI] ad removed, safe fallback";
                }
            }

            if (count == 0) return false;

            // safety net: braces / parens must still balance exactly like before
            int b0 = 0, p0 = 0, b1 = 0, p1 = 0;
            for (int i = 0; i < n; i++)
            {
                int b = Net(code[i], '{', '}'), p = Net(code[i], '(', ')');
                b0 += b; p0 += p;
                if (tag[i] == null) { b1 += b; p1 += p; }
            }
            if (b0 != b1 || p0 != p1) { count = -1; return false; }

            // Emit the modified source
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                string line = lines[i];
                if (tag[i] != null)
                {
                    int ws = line.Length - line.TrimStart().Length;
                    line = line.Substring(0, ws) + "// [TPI removed:" + tag[i] + "] " + line.Substring(ws);
                }
                sb.Append(line);
                if (inject[i] != null)
                {
                    int ws = lines[i].Length - lines[i].TrimStart().Length;
                    sb.Append(eol).Append(new string(' ', ws)).Append(inject[i]);
                }
                if (i < n - 1) sb.Append(eol);
            }
            result = sb.ToString();
            return true;
        }

        static int PrevCode(string[] code, int from)
        {
            for (int k = from - 1; k >= 0; k--) if (code[k].Trim().Length > 0) return k;
            return -1;
        }

        static int NextCode(string[] code, int from)
        {
            for (int k = from + 1; k < code.Length; k++) if (code[k].Trim().Length > 0) return k;
            return -1;
        }

        static bool StartsWithDot(string s)
        {
            s = s.TrimStart();
            return s.StartsWith(".") && !s.StartsWith("..");
        }

        static bool EndsWithContinuation(string s)
        {
            s = s.TrimEnd();
            return s.EndsWith("(") || s.EndsWith(",") || s.EndsWith("=") || s.EndsWith("+") || s.EndsWith("&&")
                || s.EndsWith("||") || s.EndsWith("?") || s.EndsWith(":") && !s.TrimStart().StartsWith("case") && !s.TrimStart().StartsWith("default")
                || s.EndsWith("=>") || s.EndsWith(".");
        }

        static int Net(string s, char open, char close)
        {
            int d = 0;
            foreach (char c in s) { if (c == open) d++; else if (c == close) d--; }
            return d;
        }

        // Removes // and /* */ comments and the contents of string / char literals. inBlock carries across lines.
        static string StripCode(string line, ref bool inBlock)
        {
            var sb = new StringBuilder(line.Length);
            for (int k = 0; k < line.Length; k++)
            {
                char c = line[k];
                char d = k + 1 < line.Length ? line[k + 1] : '\0';

                if (inBlock)
                {
                    if (c == '*' && d == '/') { inBlock = false; k++; }
                    continue;
                }
                if (c == '/' && d == '/') break;
                if (c == '/' && d == '*') { inBlock = true; k++; continue; }

                if (c == '"')
                {
                    bool verbatim = k > 0 && line[k - 1] == '@';
                    k++;
                    while (k < line.Length)
                    {
                        if (verbatim) { if (line[k] == '"') { if (k + 1 < line.Length && line[k + 1] == '"') { k += 2; continue; } break; } }
                        else
                        {
                            if (line[k] == '\\') { k += 2; continue; }
                            if (line[k] == '"') break;
                        }
                        k++;
                    }
                    sb.Append("\"\"");
                    continue;
                }
                if (c == '\'')
                {
                    k++;
                    while (k < line.Length)
                    {
                        if (line[k] == '\\') { k += 2; continue; }
                        if (line[k] == '\'') break;
                        }
                    sb.Append("''");
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
