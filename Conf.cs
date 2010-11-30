using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Conf
{
    public class Node
    {
        public string     Value    { get; set; }
        public List<Node> Children { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    ///  Syntax:
    ///    Comment  ::= '#' [^\r\n]*
    ///    WS       ::= (' ' | '\t' | '\r' | '\n')+
    ///    String   ::= [^\r\n\t #'"{}=]+ | ("'" [^']* "'") | ('"' [^"]* '"')
    ///    Node     ::= String [WS? '{' Children '}' | WS? '=' WS? Node]
    ///    Children ::= WS? (Comment | Node) (WS (Comment | Node))* WS? | WS?
    /// </remarks>
    public class Parser
    {
        string input;
        int curr;
        int end;

        Parser(string input, int start, int count)
        {
            this.input = input;
            this.curr = start;
            this.end = start + count;
        }

        public static List<Node> Parse(string text)
        {
            return new Parser(text, 0, text.Length).ReadChildren(false);
        }

        public static List<Node> Parse(string text, int start, int count)
        {
            return new Parser(text, start, count).ReadChildren(false);
        }

        public static List<Node> ParseCommandLine()
        {
            string commandLine = Environment.CommandLine;
            Parser parser = new Parser(commandLine, 0, commandLine.Length);
            string arg0;
            parser.TryReadString(out arg0); // Skip the name of the executable
            return parser.ReadChildren(false);
        }

        List<Node> ReadChildren(bool nested)
        {
            List<Node> children = new List<Node>();

            TryReadWhiteSpace();

            while (curr < end) {
                // End of nested children
                if (input[curr] == '}') {
                    if (nested) {
                        break;
                    } else {
                        throw new ParseException(curr, curr, "Superfluous '}'");
                    }
                }

                // Try to parse a comment
                if (TryReadComment()) {
                    TryReadWhiteSpace();
                } else {
                    // If it is not a comment, it must be a node
                    bool followedByWhiteSpace;
                    children.Add(ReadNode(out followedByWhiteSpace));

                    // The node has to be followed by whitespace or end of file or '}'
                    if (!followedByWhiteSpace && curr < end && input[curr] != '}') {
                        throw new ParseException(curr, curr, "Node has to be followed by whitespace");
                    }
                }
            }
            return children;
        }

        Node ReadNode(out bool followedByWhiteSpace)
        {
            string value;
            if (!TryReadString(out value))
                throw new ParseException(curr, curr, "String value expected");

            Node node = new Node();
            node.Value = value;
            followedByWhiteSpace = TryReadWhiteSpace();
            if (curr < end && input[curr] == '=') {
                curr++; // '='
                TryReadWhiteSpace();
                node.Children = new List<Node>(1);
                node.Children.Add(ReadNode(out followedByWhiteSpace));
            } else if (curr < end && input[curr] == '{') {
                curr++; // '{'
                node.Children = ReadChildren(true);
                if (curr == end || input[curr] != '}')
                    throw new ParseException(curr, curr, "'}' Expected");
                curr++; // '}'
                followedByWhiteSpace = TryReadWhiteSpace();
            }
            return node;
        }

        bool TryReadWhiteSpace()
        {
            int start = curr;
            while (curr < end && ((input[curr] <= ' ') && (input[curr] == ' ' || input[curr] == '\t' || input[curr] == '\r' || input[curr] == '\n'))) curr++;
            return curr > start;
        }

        bool TryReadComment()
        {
            if (curr < end && input[curr] == '#') {
                curr++; // '#'
                while (curr < end && input[curr] != '\r' && input[curr] != '\n') curr++;
                return true;
            } else {
                return false;
            }
        }

        bool TryReadString(out string str)
        {
            if (curr == end) {
                str = string.Empty;
                return false;
            }

            int start = curr;
            char first = input[curr];
            if (first == '"' || first == '\'') {
                // Quoted string
                curr++;
                int endQuote;
                if (curr == end || (endQuote = input.IndexOf(first, curr)) == -1)
                    throw new ParseException(start, end, "Closing quote is missing");
                curr = endQuote + 1;
                str = ReslveReferences(start + 1, endQuote);
                return true;
            } else {
                // Unquoted string
                while (curr < end) {
                    char c = input[curr];
                    if (('A' <= c && c <= 'z') || ('0' <= c && c <= '9') || c > 0x80) {
                        curr++; // Quick pass test for the most common characters
                    } else {
                        if (c == ' ' || c == '=' || c == '\r' || c == '\n' || c == '\t' || c == '{' || c == '}' || c == '#' || c == '\'' || c == '"')
                            break;  // String terminator
                        curr++; // Less common characters
                    }
                }
                str = ReslveReferences(start, curr);
                return curr > start;
            }
        }

        string ReslveReferences(int start, int end)
        {
            if (start == end)
                return string.Empty;

            if (input.IndexOf('&', start, end - start) == -1)
                return input.Substring(start, end - start);

            StringBuilder sb = new StringBuilder(end - start);
            int pos = start;
            while (pos < end) {
                int amp = input.IndexOf('&', pos, end - pos);
                if (amp == -1) {
                    sb.Append(input, pos, end - pos);
                    break;
                } else {
                    sb.Append(input, pos, amp - pos);
                    int semicolon = input.IndexOf(';', amp, end - amp);
                    if (semicolon == -1)
                        throw new ParseException(amp, end, "';' is missing after '&'");
                    string refName = input.Substring(amp + 1, semicolon - amp - 1);
                    switch (refName) {
                        case "": throw new ParseException(amp, amp + 2, "Empty reference");
                        case "amp":  sb.Append('&'); break;
                        case "apos": sb.Append('\''); break;
                        case "quot": sb.Append('\"'); break;
                        case "lt":   sb.Append('<'); break;
                        case "gt":   sb.Append('>'); break;
                        case "lb":   sb.Append('{'); break;
                        case "rb":   sb.Append('}'); break;
                        case "eq":   sb.Append('='); break;
                        case "br":   sb.Append('\n'); break;
                        case "cr":   sb.Append('\r'); break;
                        case "tab":  sb.Append('\t'); break;
                        case "nbsp": sb.Append((char)0xA0); break;
                        default:
                            // Unicode character
                            int utf32;
                            try {
                                if (refName.StartsWith("#x")) {
                                    utf32 = int.Parse(refName.Substring(2), NumberStyles.AllowHexSpecifier);
                                } else if (refName[0] == '#') {
                                    utf32 = int.Parse(refName.Substring(1), NumberStyles.None);
                                } else {
                                    utf32 = int.Parse(refName, NumberStyles.AllowHexSpecifier);
                                }
                            }
                            catch {
                                throw new ParseException(amp + 1, semicolon, "Failed to parse reference '" + refName + "'");
                            } try {
                                sb.Append(char.ConvertFromUtf32(utf32));
                            } catch {
                                throw new ParseException(amp + 1, semicolon, "Invalid Unicode code point " + refName);
                            }
                            break;
                    }
                    pos = semicolon + 1;
                }
            }
            return sb.ToString();
        }
    }

    public class ParseException : Exception
    {
        public int Start { get; private set; }
        public int End { get; private set; }

        public ParseException(int start, int end, string message)
            : base(message)
        {
            this.Start = start;
            this.End = end;
        }
    }

    public class PrettyPrinter
    {
        StringBuilder sb;
        int depth = 0;

        public string Print(IEnumerable<Node> nodes)
        {
            sb = new StringBuilder();
            Append(nodes);
            return sb.ToString();
        }

        void Append(IEnumerable<Node> nodes)
        {
            foreach (Node node in nodes) {
                // Delimiter
                if (sb.Length > 0) {
                    sb.Append("\n");
                    sb.Append(' ', depth * 2);
                }
                // The value
                Append(node.Value);
                // Children
                if (node.Children != null && node.Children.Count > 0) {
                    if (node.Children.Count == 1 && (node.Children[0].Children == null || node.Children[0].Children.Count == 0)) {
                        sb.Append(" =");
                        Append(node.Children[0].Value);
                    } else {
                        sb.Append(" {");
                        depth++;
                        Append(node.Children);
                        depth--;
                        sb.Append("\n");
                        sb.Append(' ', depth * 2);
                        sb.Append("}");
                    }
                }
            }
        }

        void Append(string val)
        {
            if (string.IsNullOrEmpty(val)) {
                sb.Append("\"\"");
            } else if (val.IndexOfAny(new char[] { '\r', '\n', '\t', ' ', '#', '\'', '"', '{', '}', '=' }) == -1) {
                sb.Append(val);
            } else {
                if (val.Contains("\"") && !val.Contains("\'")) {
                    sb.Append('\'');
                    sb.Append(val);
                    sb.Append('\'');
                } else {
                    sb.Append('\"');
                    sb.Append(val.Replace("\"", "&quot;"));
                    sb.Append('\"');
                }
            }
        }
    }

    public static class Deserializer
    {
        public static void LoadAppConfig<T>()
        {
            // Set properties from the .conf file
            string assembyPath = Assembly.GetEntryAssembly().Location;
            string confPath = Path.Combine(Path.GetDirectoryName(assembyPath), Path.GetFileNameWithoutExtension(assembyPath) + ".conf");
            if (File.Exists(confPath)) {
                string confText = File.ReadAllText(confPath);
                Deserialize(Parser.Parse(confText), default(T));
            }

            // Set properties from command line
            Deserialize(Parser.ParseCommandLine(), default(T));
        }

        public static void Deserialize<T>(List<Node> from, T to)
        {
            foreach (Node node in from) {
                FieldInfo field = typeof(T).GetField(node.Value, BindingFlags.Public | BindingFlags.Static);
                if (field != null) {
                    if (field.FieldType == typeof(TimeSpan)) {
                        TimeSpan val;
                        TimeSpan.TryParse(node.Children[0].Value, out val);
                        field.SetValue(null, val);
                    } else {
                        object val = Convert.ChangeType(node.Children[0].Value, field.FieldType);
                        field.SetValue(null, val);
                    }
                }
            }
        }
    }
}