// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: Parsers/Rtf/RtfTokenizer.cs
// Description:
//     Streaming RTF lexer. Reads the stream byte-by-byte and emits
//     RtfToken values with their absolute stream offsets.
// ==========================================================

namespace WpfHexEditor.Plugins.DocumentLoaders.Parsers.Rtf;

internal enum RtfTokenKind
{
    GroupOpen,
    GroupClose,
    ControlWord,
    ControlSymbol,
    Text,
    BinaryData,
    EndOfStream
}

internal readonly struct RtfToken
{
    public RtfTokenKind Kind      { get; init; }
    public string?      Word      { get; init; }
    public int          Parameter { get; init; }
    public string?      Text      { get; init; }
    public byte[]?      Binary    { get; init; }
    public long         Offset    { get; init; }
    public int          Length    { get; init; }
}

internal sealed class RtfTokenizer
{
    private readonly Stream   _stream;
    private readonly byte[]   _buf = new byte[1];
    private long              _pos;
    private int               _peeked = -1;
    private bool              _hasPeeked;

    public RtfTokenizer(Stream stream)
    {
        _stream = stream;
        _pos    = stream.Position;
    }

    public RtfToken NextToken(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        long start = _pos;
        int  ch    = ReadByte();

        if (ch < 0)
            return new RtfToken { Kind = RtfTokenKind.EndOfStream, Offset = start };

        switch (ch)
        {
            case '{':
                return new RtfToken { Kind = RtfTokenKind.GroupOpen, Offset = start, Length = 1 };

            case '}':
                return new RtfToken { Kind = RtfTokenKind.GroupClose, Offset = start, Length = 1 };

            case '\\':
                return ReadControlOrSymbol(start);

            case '\r':
            case '\n':
                return new RtfToken { Kind = RtfTokenKind.Text, Text = "", Offset = start, Length = 1 };

            default:
                return ReadText(start, (char)ch);
        }
    }

    private RtfToken ReadControlOrSymbol(long start)
    {
        int ch = PeekByte();
        if (ch < 0)
            return new RtfToken { Kind = RtfTokenKind.ControlSymbol, Word = "\\", Offset = start, Length = 1 };

        if (!char.IsLetter((char)ch))
        {
            ReadByte();
            return new RtfToken
            {
                Kind   = RtfTokenKind.ControlSymbol,
                Word   = ((char)ch).ToString(),
                Offset = start,
                Length = (int)(_pos - start)
            };
        }

        var word  = new System.Text.StringBuilder();
        int param = int.MinValue;

        while (PeekByte() is int c && c >= 0 && char.IsLetter((char)c))
            word.Append((char)ReadByte());

        bool negative = false;
        if (PeekByte() == '-') { ReadByte(); negative = true; }

        if (PeekByte() is int d && d >= '0' && d <= '9')
        {
            int n = 0;
            while (PeekByte() is int dd && dd >= '0' && dd <= '9')
                n = n * 10 + (ReadByte() - '0');
            param = negative ? -n : n;
        }

        if (PeekByte() == ' ') ReadByte();

        string wordStr = word.ToString().ToLowerInvariant();

        if (wordStr == "bin" && param > 0)
        {
            var data = new byte[param];
            _stream.ReadExactly(data, 0, param);
            _pos += param;
            return new RtfToken
            {
                Kind      = RtfTokenKind.BinaryData,
                Word      = wordStr,
                Parameter = param,
                Binary    = data,
                Offset    = start,
                Length    = (int)(_pos - start)
            };
        }

        return new RtfToken
        {
            Kind      = RtfTokenKind.ControlWord,
            Word      = wordStr,
            Parameter = param,
            Offset    = start,
            Length    = (int)(_pos - start)
        };
    }

    private RtfToken ReadText(long start, char first)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(first);

        while (true)
        {
            int pk = PeekByte();
            if (pk < 0 || pk == '{' || pk == '}' || pk == '\\') break;
            sb.Append((char)ReadByte());
        }

        return new RtfToken
        {
            Kind   = RtfTokenKind.Text,
            Text   = sb.ToString(),
            Offset = start,
            Length = (int)(_pos - start)
        };
    }

    private int ReadByte()
    {
        if (_hasPeeked) { _hasPeeked = false; _pos++; return _peeked; }
        int r = _stream.ReadByte();
        if (r >= 0) _pos++;
        return r;
    }

    private int PeekByte()
    {
        if (_hasPeeked) return _peeked;
        _peeked    = _stream.ReadByte();
        _hasPeeked = true;
        return _peeked;
    }
}
