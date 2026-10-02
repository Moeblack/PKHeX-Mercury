using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using PKHeX.Core;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Mercury text codec: 1- or 2-byte glyph codes terminated by 0xFF, decoded to Unicode through an
/// imported charmap (or the built-in GBA symbol table for <see cref="Default"/>).
/// <para>
/// Rules (from ROM behaviour and the research notes):
/// 0xFF terminates; 0xFA/0xFB/0xFE are reserved controls (<c>\l</c>/<c>\p</c>/<c>\n</c>) and are never
/// taken from an imported charmap; 0xFC/0xFD are represented losslessly as <c>&lt;FC&gt;</c>/<c>&lt;FD&gt;</c>;
/// any other undecodable byte is shown as <c>&lt;AB&gt;</c> and re-encoded to the same byte.
/// </para>
/// <para>
/// Text is never executed: <see cref="FromCharmapJson"/> only strips a JS assignment wrapper and parses
/// the JSON payload.
/// </para>
/// </summary>
public sealed class MercuryTextCodec
{
    public const byte TerminatorByte = 0xFF;

    private const byte ControlL = 0xFA;
    private const byte ControlP = 0xFB;
    private const byte ControlC = 0xFC;
    private const byte ControlD = 0xFD;
    private const byte ControlN = 0xFE;

    private readonly Dictionary<byte, string> _oneByte = new();
    private readonly Dictionary<ushort, string> _twoByte = new();
    private readonly Dictionary<string, byte[]> _reverse = new(StringComparer.Ordinal);
    private readonly bool _imported;

    private MercuryTextCodec(bool imported) => _imported = imported;

    /// <summary>True when a charmap was imported (as opposed to the built-in GBA symbol table).</summary>
    public bool IsImported => _imported;

    /// <summary>Number of single-byte glyphs known.</summary>
    public int SingleByteCount => _oneByte.Count;

    /// <summary>Number of double-byte glyphs known.</summary>
    public int DoubleByteCount => _twoByte.Count;

    /// <summary>
    /// Exports the imported charmap (hex code -&gt; text) for local profile storage. Returns null for the
    /// built-in default codec, which needs no storage.
    /// </summary>
    internal Dictionary<string, string>? TryExportCharmap()
    {
        if (!_imported)
            return null;
        var map = new Dictionary<string, string>(_oneByte.Count + _twoByte.Count);
        foreach ((byte code, string value) in _oneByte)
        {
            if (code == 0x00)
                continue;
            map[code.ToString("X2", CultureInfo.InvariantCulture)] = value;
        }
        foreach ((ushort code, string value) in _twoByte)
            map[((byte)(code & 0xFF)).ToString("X2", CultureInfo.InvariantCulture) +
                ((byte)(code >> 8)).ToString("X2", CultureInfo.InvariantCulture)] = value;
        return map;
    }

    /// <summary>
    /// Built-in codec using PKHeX's Gen3 (English) printable symbol table. It only knows the base GBA
    /// symbol set; CJK text requires an imported charmap.
    /// </summary>
    public static MercuryTextCodec Default()
    {
        var codec = new MercuryTextCodec(imported: false);
        for (int b = 0; b <= 0xFE; b++)
        {
            char c = StringConverter3.GetG3Char((byte)b, (int)LanguageID.English);
            if (c == '\0' || c == (char)TerminatorByte)
                continue;
            codec.Add((byte)b, c.ToString());
        }
        codec.Add(0x00, " ");
        return codec;
    }

    /// <summary>
    /// Builds a codec from a plain hex-&gt;text object, a full game-data JSON containing a
    /// <c>charmap</c> property, or a <c>game_data.js</c>/<c>app.html</c> assignment wrapper such as
    /// <c>window.SAVE_HOME_GAME_DATA={...,"charmap":{...}}</c>. The JS wrapper is stripped textually and
    /// is never executed.
    /// </summary>
    public static MercuryTextCodec FromCharmapJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        string payload = ExtractJsonPayload(json);
        using var document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;

        JsonElement map;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("charmap", out JsonElement embedded) &&
            embedded.ValueKind == JsonValueKind.Object)
        {
            map = embedded;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            map = root;
        }
        else
        {
            throw new ArgumentException("Charmap JSON must be an object (or contain a 'charmap' object).", nameof(json));
        }

        var codec = new MercuryTextCodec(imported: true);
        foreach (JsonProperty property in map.EnumerateObject())
        {
            if (!TryParseHex(property.Name, out ushort code, out int byteLength))
                continue;
            if (property.Value.ValueKind != JsonValueKind.String)
                continue;
            string value = property.Value.GetString() ?? string.Empty;
            if (value.Length == 0)
                continue;

            if (byteLength == 1)
            {
                byte b = (byte)code;
                if (IsReserved(b))
                    continue; // never let an external map overwrite controls/terminator
                codec.Add(b, value);
            }
            else
            {
                // Only the leading byte participates in control dispatch, so only it must stay clear
                // of the reserved range. Trailing bytes are consumed by the two-byte lookup.
                if (IsReserved((byte)(code & 0xFF)))
                    continue;
                codec.Add(code, value);
            }
        }

        codec.Add(0x00, " ");
        return codec;
    }

    /// <summary>
    /// Decodes a glyph sequence, stopping at 0xFF. Unknown bytes are emitted as <c>&lt;AB&gt;</c>.
    /// </summary>
    public string Decode(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b == TerminatorByte)
                break;

            if (TryGetControlText(b, out string? control))
            {
                builder.Append(control);
                continue;
            }

            if (i + 1 < bytes.Length)
            {
                ushort pair = (ushort)(b | (bytes[i + 1] << 8));
                if (_twoByte.TryGetValue(pair, out string? two))
                {
                    builder.Append(two);
                    i++;
                    continue;
                }
            }

            if (_oneByte.TryGetValue(b, out string? one))
            {
                builder.Append(one);
                continue;
            }

            AppendByteToken(builder, b);
        }
        return builder.ToString();
    }

    /// <summary>
    /// Encodes <paramref name="text"/> into exactly <paramref name="byteLength"/> bytes.
    /// Unused space is filled with 0xFF (terminator + padding). Throws when the text cannot be encoded
    /// or exceeds the byte budget.
    /// </summary>
    public byte[] Encode(string text, int byteLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);

        if (!TryEncodeCore(text, out byte[]? encoded, out string? error))
            throw new ArgumentException(error, nameof(text));
        if (encoded.Length > byteLength)
            throw new ArgumentException(
                $"Encoded text is {encoded.Length} bytes but the field only allows {byteLength}.", nameof(byteLength));

        var result = new byte[byteLength];
        encoded.CopyTo(result, 0);
        for (int i = encoded.Length; i < byteLength; i++)
            result[i] = TerminatorByte;
        return result;
    }

    /// <summary>
    /// Returns true when <paramref name="text"/> can be encoded losslessly into at most
    /// <paramref name="byteLength"/> bytes (measured in bytes, not Unicode characters).
    /// </summary>
    public bool CanEncode(string text, int byteLength)
    {
        if (text is null || byteLength <= 0)
            return false;
        return TryEncodeCore(text, out byte[]? encoded, out _) && encoded.Length <= byteLength;
    }

    private bool TryEncodeCore(string text, out byte[] encoded, out string? error)
    {
        encoded = [];
        error = null;
        var output = new List<byte>(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '\\' && i + 1 < text.Length)
            {
                char next = text[i + 1];
                byte? control = next switch
                {
                    'l' => ControlL,
                    'p' => ControlP,
                    'n' => ControlN,
                    _ => null,
                };
                if (control is not null)
                {
                    output.Add(control.Value);
                    i++;
                    continue;
                }
            }

            if (c == '<' && TryReadByteToken(text, i, out byte token, out int consumed))
            {
                output.Add(token);
                i += consumed;
                continue;
            }

            if (_reverse.TryGetValue(c.ToString(), out byte[]? glyph))
            {
                output.AddRange(glyph);
                continue;
            }

            error = $"Character '{c}' (U+{(int)c:X4}) cannot be encoded by the current codec.";
            return false;
        }

        encoded = [.. output];
        return true;
    }

    private static bool TryReadByteToken(string text, int start, out byte value, out int consumed)
    {
        value = 0;
        consumed = 0;
        // layout: '<' X X '>'
        if (start + 3 >= text.Length || text[start + 3] != '>')
            return false;
        if (!TryParseHexByte(text[start + 1], out int hi) || !TryParseHexByte(text[start + 2], out int lo))
            return false;
        value = (byte)((hi << 4) | lo);
        if (value == TerminatorByte)
            return false; // 0xFF terminates, it is not a glyph token
        consumed = 4;
        return true;
    }

    private void Add(byte code, string value)
    {
        _oneByte[code] = value;
        RegisterReverse(value, [code]);
    }

    private void Add(ushort code, string value)
    {
        _twoByte[code] = value;
        RegisterReverse(value, [(byte)(code & 0xFF), (byte)(code >> 8)]);
    }

    private void RegisterReverse(string value, byte[] bytes)
    {
        if (_reverse.TryGetValue(value, out byte[]? existing) && existing.Length <= bytes.Length)
            return; // prefer the shortest encoding
        _reverse[value] = bytes;
    }

    private static bool IsReserved(byte b) => b is ControlL or ControlP or ControlC or ControlD or ControlN or TerminatorByte;

    private static bool TryGetControlText(byte b, out string? text)
    {
        switch (b)
        {
            case ControlL: text = "\\l"; return true;
            case ControlP: text = "\\p"; return true;
            case ControlC: text = "<FC>"; return true;
            case ControlD: text = "<FD>"; return true;
            case ControlN: text = "\\n"; return true;
            default: text = null; return false;
        }
    }

    private static void AppendByteToken(StringBuilder builder, byte b)
    {
        builder.Append('<');
        builder.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        builder.Append('>');
    }

    private static bool TryParseHex(string key, out ushort code, out int byteLength)
    {
        // Two-byte keys are written in stream order; the first stream byte is the low byte of the
        // stored key so that Decode's (b | next << 8) lookup matches directly.
        code = 0;
        byteLength = 0;
        if (string.IsNullOrEmpty(key))
            return false;
        key = key.Trim();
        if (key.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            key = key[2..];
        if (key.Length == 2)
        {
            if (!TryParseHexByte(key[0], out int hi) || !TryParseHexByte(key[1], out int lo))
                return false;
            code = (ushort)((hi << 4) | lo);
            byteLength = 1;
            return true;
        }
        if (key.Length == 4)
        {
            if (!TryParseHexByte(key[0], out int h0) || !TryParseHexByte(key[1], out int l0) ||
                !TryParseHexByte(key[2], out int h1) || !TryParseHexByte(key[3], out int l1))
                return false;
            int first = (h0 << 4) | l0;
            int second = (h1 << 4) | l1;
            code = (ushort)(first | (second << 8));
            byteLength = 2;
            return true;
        }
        return false;
    }

    private static bool TryParseHexByte(char c, out int value)
    {
        value = 0;
        if (c is >= '0' and <= '9') { value = c - '0'; return true; }
        if (c is >= 'a' and <= 'f') { value = c - 'a' + 10; return true; }
        if (c is >= 'A' and <= 'F') { value = c - 'A' + 10; return true; }
        return false;
    }

    /// <summary>
    /// Extracts the JSON payload from either raw JSON or a single JS assignment/HTML script without
    /// executing anything. Uses brace matching that ignores braces inside strings.
    /// </summary>
    internal static string ExtractJsonPayload(string text)
    {
        // Prefer the known assignment marker so CSS/HTML braces before the script are ignored.
        int marker = text.IndexOf("SAVE_HOME_GAME_DATA", StringComparison.Ordinal);
        int searchFrom = 0;
        if (marker >= 0)
        {
            int equals = text.IndexOf('=', marker);
            searchFrom = equals >= 0 ? equals + 1 : marker;
        }

        int start = text.IndexOf('{', searchFrom);
        if (start < 0)
            throw new ArgumentException("No JSON object found in the supplied text.", nameof(text));

        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                        return text[start..(i + 1)];
                    break;
            }
        }

        throw new ArgumentException("Unbalanced JSON object in the supplied text.", nameof(text));
    }
}
