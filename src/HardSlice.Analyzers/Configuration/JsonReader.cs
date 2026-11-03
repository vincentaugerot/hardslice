using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;

namespace HardSlice.Analyzers.Configuration;

// hardslice.json is small and closed: objects, arrays and strings. Compiler hosts do not all load System.Text.Json
// for a netstandard2.0 analyzer, so this reader parses strict JSON, plus the comments the architecture tests accept,
// and keeps the span of every value so that a diagnostic can point at the offending key.
internal static class JsonReader
{
    // hardslice.json nests three levels; a deeper file is refused before the recursion of the parser can exhaust the stack
    // of the compiler or of the IDE.
    public const int MaxDepth = 64;

    private static readonly Regex Number = new(@"\G-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant);

    public static JsonNode Parse(string text)
    {
        var parser = new Parser(text);
        var value = parser.ParseValue();
        parser.ExpectEnd();
        return value;
    }

    private sealed class Parser(string text)
    {
        private int _position;
        private int _depth;

        public JsonNode ParseValue()
        {
            SkipTrivia();
            if (_position >= text.Length)
                throw Error("a value is missing");

            return text[_position] switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                '"' => ParseString(),
                _ => ParseLiteral()
            };
        }

        public void ExpectEnd()
        {
            SkipTrivia();
            if (_position < text.Length)
                throw Error($"unexpected '{text[_position]}' after the root value");
        }

        private JsonObject ParseObject()
        {
            var start = Enter();
            var properties = ImmutableArray.CreateBuilder<JsonProperty>();
            SkipTrivia();
            if (TryConsume('}'))
                return Leave(new JsonObject(properties.ToImmutable(), TextSpan.FromBounds(start, _position)));

            while (true)
            {
                SkipTrivia();
                if (_position >= text.Length || text[_position] != '"')
                    throw Error("expected a property name in double quotes");

                var name = ParseString();
                SkipTrivia();
                if (!TryConsume(':'))
                    throw Error("expected ':' after a property name");

                properties.Add(new JsonProperty(name.Value, name.Span, ParseValue()));
                SkipTrivia();
                if (TryConsume('}'))
                    return Leave(new JsonObject(properties.ToImmutable(), TextSpan.FromBounds(start, _position)));
                if (!TryConsume(','))
                    throw Error("expected ',' or '}' after a property value");
            }
        }

        private JsonArray ParseArray()
        {
            var start = Enter();
            var items = ImmutableArray.CreateBuilder<JsonNode>();
            SkipTrivia();
            if (TryConsume(']'))
                return Leave(new JsonArray(items.ToImmutable(), TextSpan.FromBounds(start, _position)));

            while (true)
            {
                items.Add(ParseValue());
                SkipTrivia();
                if (TryConsume(']'))
                    return Leave(new JsonArray(items.ToImmutable(), TextSpan.FromBounds(start, _position)));
                if (!TryConsume(','))
                    throw Error("expected ',' or ']' after an array item");
            }
        }

        private JsonString ParseString()
        {
            var start = _position++;
            var value = new StringBuilder();
            while (_position < text.Length)
            {
                var current = text[_position++];
                if (current == '"')
                    return new JsonString(value.ToString(), TextSpan.FromBounds(start, _position));
                if (current < ' ')
                    throw Error("control character in a string", _position - 1);
                if (current != '\\')
                {
                    value.Append(current);
                    continue;
                }

                if (_position >= text.Length)
                    break;

                var escaped = text[_position++];
                switch (escaped)
                {
                    case '"' or '\\' or '/':
                        value.Append(escaped);
                        break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u' when _position + 4 <= text.Length
                        && int.TryParse(text.Substring(_position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code):
                        value.Append((char)code);
                        _position += 4;
                        break;
                    default:
                        throw Error("invalid escape sequence in a string", _position - 2);
                }
            }

            throw Error("unterminated string", start);
        }

        private JsonLiteral ParseLiteral()
        {
            var start = _position;
            foreach (var keyword in new[] { "true", "false", "null" })
            {
                if (string.CompareOrdinal(text, _position, keyword, 0, keyword.Length) == 0)
                {
                    _position += keyword.Length;
                    return new JsonLiteral(TextSpan.FromBounds(start, _position));
                }
            }

            var number = Number.Match(text, _position);
            if (!number.Success || number.Length == 0)
                throw Error($"unexpected '{text[_position]}'");

            _position += number.Length;
            return new JsonLiteral(TextSpan.FromBounds(start, _position));
        }

        // Opens an object or an array, and returns its start.
        private int Enter()
        {
            if (++_depth > MaxDepth)
                throw Error($"nesting deeper than {MaxDepth} levels");

            return _position++;
        }

        private T Leave<T>(T node)
        {
            _depth--;
            return node;
        }

        private bool TryConsume(char expected)
        {
            if (_position >= text.Length || text[_position] != expected)
                return false;

            _position++;
            return true;
        }

        private void SkipTrivia()
        {
            while (_position < text.Length)
            {
                var current = text[_position];
                if (current is ' ' or '\t' or '\r' or '\n')
                {
                    _position++;
                }
                else if (current == '/' && _position + 1 < text.Length && text[_position + 1] == '/')
                {
                    while (_position < text.Length && text[_position] != '\n')
                        _position++;
                }
                else if (current == '/' && _position + 1 < text.Length && text[_position + 1] == '*')
                {
                    var end = text.IndexOf("*/", _position + 2, StringComparison.Ordinal);
                    if (end < 0)
                        throw Error("unterminated comment");
                    _position = end + 2;
                }
                else
                {
                    return;
                }
            }
        }

        private JsonFormatException Error(string reason, int? position = null) => new(reason, position ?? _position);
    }
}

internal abstract class JsonNode(TextSpan span)
{
    public TextSpan Span { get; } = span;
}

internal sealed class JsonObject(ImmutableArray<JsonProperty> properties, TextSpan span) : JsonNode(span)
{
    public ImmutableArray<JsonProperty> Properties { get; } = properties;
}

internal sealed class JsonProperty(string name, TextSpan nameSpan, JsonNode value)
{
    public string Name { get; } = name;

    public TextSpan NameSpan { get; } = nameSpan;

    public JsonNode Value { get; } = value;
}

internal sealed class JsonArray(ImmutableArray<JsonNode> items, TextSpan span) : JsonNode(span)
{
    public ImmutableArray<JsonNode> Items { get; } = items;
}

internal sealed class JsonString(string value, TextSpan span) : JsonNode(span)
{
    public string Value { get; } = value;
}

// A number, true, false or null: valid JSON, but never a valid value in hardslice.json.
internal sealed class JsonLiteral(TextSpan span) : JsonNode(span);

internal sealed class JsonFormatException(string reason, int position) : Exception(reason)
{
    public int Position { get; } = position;
}
