using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RrHarness
{
    /// <summary>
    /// A small JSON reader for scenario files: objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;,
    /// numbers double, plus string, bool and null. Comments (// to end of line) are allowed so scenarios can be
    /// annotated. No dependency on any package.
    /// </summary>
    public static class RrJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object v = Value(text, ref i);
            Skip(text, ref i);

            if (i < text.Length)
            {
                throw new FormatException("trailing characters at " + i);
            }

            return v;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                }
                else if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    while (i < s.Length && s[i] != '\n')
                    {
                        i++;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);

            if (i >= s.Length)
            {
                throw new FormatException("unexpected end");
            }

            char c = s[i];

            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Skip(s, ref i);

                if (s[i] == '}')
                {
                    i++;
                    return d;
                }

                while (true)
                {
                    Skip(s, ref i);
                    string key = Str(s, ref i);
                    Skip(s, ref i);
                    Expect(s, ref i, ':');
                    d[key] = Value(s, ref i);
                    Skip(s, ref i);

                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    Expect(s, ref i, '}');
                    return d;
                }
            }

            if (c == '[')
            {
                var l = new List<object>();
                i++;
                Skip(s, ref i);

                if (s[i] == ']')
                {
                    i++;
                    return l;
                }

                while (true)
                {
                    l.Add(Value(s, ref i));
                    Skip(s, ref i);

                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    Expect(s, ref i, ']');
                    return l;
                }
            }

            if (c == '"')
            {
                return Str(s, ref i);
            }

            if (Word(s, ref i, "true"))
            {
                return true;
            }

            if (Word(s, ref i, "false"))
            {
                return false;
            }

            if (Word(s, ref i, "null"))
            {
                return null;
            }

            int start = i;

            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
            {
                i++;
            }

            if (i == start)
            {
                throw new FormatException("unexpected '" + c + "' at " + i);
            }

            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static bool Word(string s, ref int i, string w)
        {
            if (string.CompareOrdinal(s, i, w, 0, w.Length) == 0)
            {
                i += w.Length;
                return true;
            }

            return false;
        }

        private static void Expect(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c)
            {
                throw new FormatException("expected '" + c + "' at " + i);
            }

            i++;
        }

        private static string Str(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var sb = new StringBuilder();

            while (i < s.Length && s[i] != '"')
            {
                char c = s[i++];

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                char e = s[i++];

                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }

            Expect(s, ref i, '"');
            return sb.ToString();
        }
    }
}
