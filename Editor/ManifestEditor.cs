using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TPIntegrations.Editor
{
    /// <summary>Reads / edits Packages/manifest.json (no external JSON library needed).</summary>
    public static class ManifestEditor
    {
        static string ManifestPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));

        public static HashSet<string> ReadDependencies()
        {
            var set = new HashSet<string>();
            try
            {
                var root = MiniJson.Parse(File.ReadAllText(ManifestPath)) as Dictionary<string, object>;
                if (root != null && root.TryGetValue("dependencies", out var d) && d is Dictionary<string, object> deps)
                    foreach (var k in deps.Keys) set.Add(k);
            }
            catch (Exception e) { Debug.LogWarning("[TPI] Could not read manifest.json: " + e.Message); }
            return set;
        }

        /// <summary>Adds a scoped registry (or merges scopes into an existing one). Returns true if the file changed.</summary>
        public static bool EnsureScopedRegistry(string name, string url, IEnumerable<string> scopes)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(ManifestPath));

            List<object> regs = null;
            if (root.TryGetValue("scopedRegistries", out var r)) regs = r as List<object>;
            if (regs == null) { regs = new List<object>(); root["scopedRegistries"] = regs; }

            bool changed = false;
            Dictionary<string, object> reg = null;
            foreach (var o in regs)
            {
                if (o is Dictionary<string, object> d && d.TryGetValue("url", out var u) &&
                    string.Equals((u as string)?.TrimEnd('/'), url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                { reg = d; break; }
            }
            if (reg == null)
            {
                reg = new Dictionary<string, object>
                {
                    { "name", name }, { "url", url }, { "scopes", new List<object>() }
                };
                regs.Add(reg);
                changed = true;
            }

            List<object> sc = reg.TryGetValue("scopes", out var s) ? s as List<object> : null;
            if (sc == null) { sc = new List<object>(); reg["scopes"] = sc; changed = true; }
            foreach (var scope in scopes)
                if (!sc.Contains(scope)) { sc.Add(scope); changed = true; }

            if (changed)
            {
                File.Copy(ManifestPath, ManifestPath + ".bak", true);   // safety backup
                File.WriteAllText(ManifestPath, MiniJson.Serialize(root));
            }
            return changed;
        }
    }

    /// <summary>Tiny JSON parser/serializer (objects, arrays, strings, numbers, bool, null).</summary>
    public static class MiniJson
    {
        public static object Parse(string s) { int i = 0; return Val(s, ref i); }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Val(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string k = Str(s, ref i);
                    Ws(s, ref i); i++; // ':'
                    d[k] = Val(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d;
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Val(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l;
                }
            }
            if (c == '"') return Str(s, ref i);

            int st = i;
            while (i < s.Length && ",}] \n\r\t".IndexOf(s[i]) < 0) i++;
            string t = s.Substring(st, i - st);
            if (t == "true") return true;
            if (t == "false") return false;
            if (t == "null") return null;
            return double.Parse(t, CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else sb.Append(s[i]);
                i++;
            }
            i++; // closing quote
            return sb.ToString();
        }

        public static string Serialize(object o) { var sb = new StringBuilder(); Write(sb, o, 0); sb.Append('\n'); return sb.ToString(); }

        static void Write(StringBuilder sb, object o, int ind)
        {
            switch (o)
            {
                case null: sb.Append("null"); break;
                case string str: WriteStr(sb, str); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double num: sb.Append(num.ToString("R", CultureInfo.InvariantCulture)); break;
                case Dictionary<string, object> dict:
                    if (dict.Count == 0) { sb.Append("{}"); break; }
                    sb.Append("{\n");
                    int n = 0;
                    foreach (var kv in dict)
                    {
                        sb.Append(' ', (ind + 1) * 2);
                        WriteStr(sb, kv.Key); sb.Append(": ");
                        Write(sb, kv.Value, ind + 1);
                        if (++n < dict.Count) sb.Append(',');
                        sb.Append('\n');
                    }
                    sb.Append(' ', ind * 2).Append('}');
                    break;
                case List<object> list:
                    if (list.Count == 0) { sb.Append("[]"); break; }
                    sb.Append("[\n");
                    for (int j = 0; j < list.Count; j++)
                    {
                        sb.Append(' ', (ind + 1) * 2);
                        Write(sb, list[j], ind + 1);
                        if (j < list.Count - 1) sb.Append(',');
                        sb.Append('\n');
                    }
                    sb.Append(' ', ind * 2).Append(']');
                    break;
                default: WriteStr(sb, o.ToString()); break;
            }
        }

        static void WriteStr(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
