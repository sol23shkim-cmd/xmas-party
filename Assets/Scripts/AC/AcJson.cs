// AcJson.cs — 작은 JSON 읽기 (Firebase가 보내는 임의 모양 데이터용). 결과: Dictionary<string,object>, List<object>, double, string, bool, null
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class AcJson
{
    public static object Parse(string s) { int i = 0; return Val(s, ref i); }

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object Val(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length) return null;
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++;
            Ws(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (i < s.Length)
            {
                Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                d[k] = Val(s, ref i); Ws(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; break; }
            }
            return d;
        }
        if (c == '[')
        {
            var l = new List<object>(); i++;
            Ws(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (i < s.Length)
            {
                l.Add(Val(s, ref i)); Ws(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; break; }
            }
            return l;
        }
        if (c == '"') return Str(s, ref i);
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int st = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out var num);
        return num;
    }

    static string Str(string s, ref int i)
    {
        var sb = new StringBuilder();
        if (i < s.Length && s[i] == '"') i++;
        while (i < s.Length && s[i] != '"')
        {
            char c = s[i++];
            if (c == '\\' && i < s.Length)
            {
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break; case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            else sb.Append(c);
        }
        i++;
        return sb.ToString();
    }

    public static string Esc(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in s ?? "")
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }

    public static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v != null ? v.ToString() : "";
    public static float F(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is double x ? (float)x : 0f;
    public static double D(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is double x ? x : 0;
    public static bool B(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is bool x && x;
}
