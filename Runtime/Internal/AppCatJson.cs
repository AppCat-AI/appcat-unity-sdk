using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AppCat.Internal
{
    /// <summary>
    /// Dependency-free JSON codec used on the native bridge boundary (Swift
    /// and Kotlin hand results to C# as JSON strings). Decoded shapes:
    /// Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double, long, bool, null.
    /// Never throws: encode returns null and decode returns null on failure.
    /// </summary>
    internal static class AppCatJson
    {
        private const int MaxDepth = 64;

        public static string Encode(object value)
        {
            try
            {
                var sb = new StringBuilder();
                Write(sb, value, 0);
                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static object Decode(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var p = new Parser(json);
                var v = p.ParseValue();
                p.SkipWhitespace();
                return p.AtEnd ? v : null;
            }
            catch
            {
                return null;
            }
        }

        public static Dictionary<string, object> DecodeObject(string json)
        {
            return Decode(json) as Dictionary<string, object>;
        }

        // ---- writer -----------------------------------------------------

        private static void Write(StringBuilder sb, object value, int depth)
        {
            if (depth > MaxDepth) throw new InvalidOperationException("depth");
            switch (value)
            {
                case null: sb.Append("null"); return;
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case IDictionary dict:
                    sb.Append('{');
                    var firstKey = true;
                    foreach (DictionaryEntry e in dict)
                    {
                        if (!(e.Key is string k)) continue;
                        if (!firstKey) sb.Append(',');
                        firstKey = false;
                        WriteString(sb, k);
                        sb.Append(':');
                        Write(sb, e.Value, depth + 1);
                    }
                    sb.Append('}');
                    return;
                case IEnumerable list:
                    sb.Append('[');
                    var first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, item, depth + 1);
                    }
                    sb.Append(']');
                    return;
                case double d:
                    sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case float f:
                    sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : ((double)f).ToString("R", CultureInfo.InvariantCulture));
                    return;
            }

            if (value is sbyte || value is byte || value is short || value is ushort || value is int
                || value is uint || value is long || value is ulong || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- parser -----------------------------------------------------

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;
            private int _depth;

            public Parser(string s) { _s = s; }
            public bool AtEnd => _i >= _s.Length;

            public void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                var c = Peek();
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw new FormatException();
                }
            }

            private void Expect(string literal)
            {
                if (string.CompareOrdinal(_s, _i, literal, 0, literal.Length) != 0) throw new FormatException();
                _i += literal.Length;
            }

            private Dictionary<string, object> ParseObject()
            {
                if (++_depth > MaxDepth) throw new FormatException();
                var dict = new Dictionary<string, object>();
                _i++;
                SkipWhitespace();
                if (Peek() == '}') { _i++; _depth--; return dict; }
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw new FormatException();
                    var key = ParseString();
                    SkipWhitespace();
                    if (Peek() != ':') throw new FormatException();
                    _i++;
                    dict[key] = ParseValue();
                    SkipWhitespace();
                    var c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == '}') { _i++; break; }
                    throw new FormatException();
                }
                _depth--;
                return dict;
            }

            private List<object> ParseArray()
            {
                if (++_depth > MaxDepth) throw new FormatException();
                var list = new List<object>();
                _i++;
                SkipWhitespace();
                if (Peek() == ']') { _i++; _depth--; return list; }
                while (true)
                {
                    list.Add(ParseValue());
                    SkipWhitespace();
                    var c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == ']') { _i++; break; }
                    throw new FormatException();
                }
                _depth--;
                return list;
            }

            private string ParseString()
            {
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw new FormatException();
                    var c = _s[_i++];
                    if (c == '"') break;
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw new FormatException();
                    var e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw new FormatException();
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw new FormatException();
                    }
                }
                return sb.ToString();
            }

            private object ParseNumber()
            {
                var start = _i;
                if (Peek() == '-') _i++;
                while (!AtEnd && char.IsDigit(_s[_i])) _i++;
                var isFloat = false;
                if (!AtEnd && _s[_i] == '.')
                {
                    isFloat = true; _i++;
                    while (!AtEnd && char.IsDigit(_s[_i])) _i++;
                }
                if (!AtEnd && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    isFloat = true; _i++;
                    if (!AtEnd && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    while (!AtEnd && char.IsDigit(_s[_i])) _i++;
                }
                var text = _s.Substring(start, _i - start);
                if (!isFloat && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
                return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            private char Peek()
            {
                if (AtEnd) throw new FormatException();
                return _s[_i];
            }
        }
    }
}
