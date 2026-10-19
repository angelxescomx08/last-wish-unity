using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LastWish.Tests
{
    /// <summary>Tiny JSON reader for the Python oracle fixtures (objects → Dictionary, arrays → List).</summary>
    public static class FixtureJson
    {
        public static string Path(string name)
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (var folder = new DirectoryInfo(start); folder != null; folder = folder.Parent)
                {
                    var path = System.IO.Path.Combine(folder.FullName, "Assets/Tests/EditMode/Fixtures/" + name);
                    if (File.Exists(path)) return path;
                }
            throw new FileNotFoundException("Missing generated Python oracle " + name);
        }

        public static object Load(string name) => Parse(File.ReadAllText(Path(name), Encoding.UTF8));

        public static object Parse(string text) { var i = 0; var value = Value(text, ref i); return value; }

        private static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            var c = s[i];
            if (c == '{')
            {
                var result = new Dictionary<string, object>(); i++; Skip(s, ref i);
                if (s[i] == '}') { i++; return result; }
                while (true)
                {
                    Skip(s, ref i); var key = (string)Value(s, ref i); Skip(s, ref i); i++; // ':'
                    result[key] = Value(s, ref i); Skip(s, ref i);
                    if (s[i++] == '}') return result;
                }
            }
            if (c == '[')
            {
                var result = new List<object>(); i++; Skip(s, ref i);
                if (s[i] == ']') { i++; return result; }
                while (true)
                {
                    result.Add(Value(s, ref i)); Skip(s, ref i);
                    if (s[i++] == ']') return result;
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
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
            var start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            var number = s.Substring(start, i - start);
            if (number.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0) return double.Parse(number, CultureInfo.InvariantCulture);
            return long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? (object)l
                : System.Numerics.BigInteger.Parse(number, CultureInfo.InvariantCulture);
        }

        public static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
        public static List<object> Arr(object o) => (List<object>)o;
        public static long Long(object o) => o is long l ? l : Convert.ToInt64(o, CultureInfo.InvariantCulture);
        public static int Int(object o) => checked((int)Long(o));
        public static string Str(object o) => o as string;
        public static List<int> Ints(object o) { var r = new List<int>(); foreach (var v in Arr(o)) r.Add(Int(v)); return r; }
        public static List<string> Strs(object o) { var r = new List<string>(); foreach (var v in Arr(o)) r.Add(v as string); return r; }
    }
}
