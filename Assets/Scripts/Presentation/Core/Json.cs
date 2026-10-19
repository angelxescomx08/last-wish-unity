using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LastWish.Presentation
{
    /// <summary>Small allocation-tolerant JSON reader/writer for metadata and settings (load time only).</summary>
    public static class Json
    {
        public static object Parse(string text) { var i = 0; return Value(text, ref i); }

        private static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            var c = s[i];
            if (c == '{')
            {
                var result = new Dictionary<string, object>(); i++; Skip(s, ref i);
                if (s[i] == '}') { i++; return result; }
                while (true)
                {
                    Skip(s, ref i); var key = (string)Value(s, ref i); Skip(s, ref i);
                    if (s[i] != ':') throw new FormatException("Expected ':'"); i++;
                    result[key] = Value(s, ref i); Skip(s, ref i);
                    var next = s[i++]; if (next == '}') return result; if (next != ',') throw new FormatException("Expected ','");
                }
            }
            if (c == '[')
            {
                var result = new List<object>(); i++; Skip(s, ref i);
                if (s[i] == ']') { i++; return result; }
                while (true)
                {
                    result.Add(Value(s, ref i)); Skip(s, ref i);
                    var next = s[i++]; if (next == ']') return result; if (next != ',') throw new FormatException("Expected ','");
                }
            }
            if (c == '"')
            {
                var sb = new StringBuilder(); i++;
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
                        i++;
                    }
                    else sb.Append(s[i++]);
                }
                i++; return sb.ToString();
            }
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            var start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            var number = s.Substring(start, i - start);
            if (number.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0) return double.Parse(number, CultureInfo.InvariantCulture);
            return long.Parse(number, CultureInfo.InvariantCulture);
        }

        public static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;
        public static List<object> Arr(object o) => o as List<object>;
        public static double Num(object o, double fallback = 0) => o is long l ? l : o is double d ? d : fallback;
        public static int Int(object o, int fallback = 0) => o is long l ? (int)l : o is double d ? (int)d : fallback;
        public static string Str(object o) => o as string;
        public static object Get(this Dictionary<string, object> d, string key) => d != null && d.TryGetValue(key, out var v) ? v : null;
        public static int[] Ints(object o) { var a = Arr(o); if (a == null) return null; var r = new int[a.Count]; for (var i = 0; i < a.Count; i++) r[i] = Int(a[i]); return r; }
        public static double[] Nums(object o) { var a = Arr(o); if (a == null) return null; var r = new double[a.Count]; for (var i = 0; i < a.Count; i++) r[i] = Num(a[i]); return r; }

        public static string Write(object value)
        {
            var sb = new StringBuilder(); WriteValue(sb, value, 0); return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, int depth)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s:
                    sb.Append('"');
                    foreach (var ch in s)
                    {
                        if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                        else if (ch == '\n') sb.Append("\\n");
                        else if (ch < 32) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                    }
                    sb.Append('"'); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); break;
                case double d:
                    var text = d.ToString("R", CultureInfo.InvariantCulture);
                    if (text.IndexOfAny(new[] { '.', 'E', 'e' }) < 0) text += ".0";
                    sb.Append(text); break;
                case IDictionary<string, object> dict:
                    sb.Append("{\n"); var first = true;
                    foreach (var pair in dict)
                    {
                        if (!first) sb.Append(",\n"); first = false;
                        sb.Append(' ', 2 * (depth + 1)); WriteValue(sb, pair.Key, depth + 1); sb.Append(": "); WriteValue(sb, pair.Value, depth + 1);
                    }
                    sb.Append('\n').Append(' ', 2 * depth).Append('}'); break;
                case System.Collections.IEnumerable list:
                    sb.Append('['); var f2 = true;
                    foreach (var item in list) { if (!f2) sb.Append(", "); f2 = false; WriteValue(sb, item, depth + 1); }
                    sb.Append(']'); break;
                default: sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
            }
        }
    }
}
