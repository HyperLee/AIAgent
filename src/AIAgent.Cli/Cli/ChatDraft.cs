using System.Globalization;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal sealed class ChatDraft : IDisposable
{
    private readonly char[] _buffer = new char[ChatInputValidation.MaxCodeUnits];
    private int _length;
    private char? _pending;
    internal int Cursor { get; private set; }
    internal bool Invalid { get; private set; }
    internal string Text => new(_buffer, 0, _length);
    internal void Invalidate() { Invalid = true; _pending = null; }
    internal void FinishCharacter() { if (_pending is not null) Invalidate(); }
    internal void Character(char c)
    {
        if (Invalid) return;
        if (_pending is { } high)
        {
            _pending = null;
            if (!char.IsLowSurrogate(c)) { Invalidate(); return; }
            Insert(new string([high, c])); return;
        }
        if (char.IsHighSurrogate(c)) { _pending = c; return; }
        if (char.IsLowSurrogate(c) || (char.IsControl(c) && c is not ('\n' or '\t')) || c is '\u2028' or '\u2029')
        { Invalidate(); return; }
        Insert(c.ToString());
    }
    private void Insert(string value)
    {
        if (_length + value.Length > _buffer.Length) { Invalidate(); return; }
        Array.Copy(_buffer, Cursor, _buffer, Cursor + value.Length, _length - Cursor);
        value.AsSpan().CopyTo(_buffer.AsSpan(Cursor));
        _length += value.Length; Cursor += value.Length;
        if (Cursor < _length) SnapCursor();
    }
    private int[] Boundaries() => [.. StringInfo.ParseCombiningCharacters(Text), _length];
    private void SnapCursor()
    {
        foreach (var boundary in Boundaries()) if (boundary >= Cursor) { Cursor = boundary; return; }
    }
    internal void Move(ConsoleKey key)
    {
        FinishCharacter(); if (Invalid) return;
        var text = Text; var boundaries = Boundaries();
        var start = Cursor == 0 ? 0 : text.LastIndexOf('\n', Cursor - 1) + 1;
        var end = text.IndexOf('\n', Cursor); if (end < 0) end = _length;
        switch (key)
        {
            case ConsoleKey.LeftArrow: Cursor = boundaries.LastOrDefault(b => b < Cursor); break;
            case ConsoleKey.RightArrow: Cursor = boundaries.FirstOrDefault(b => b > Cursor, _length); break;
            case ConsoleKey.Home: Cursor = start; break;
            case ConsoleKey.End: Cursor = end; break;
            case ConsoleKey.UpArrow:
                if (start == 0) break;
                var previousStart = start <= 1 ? 0 : text.LastIndexOf('\n', start - 2) + 1;
                MoveLine(boundaries, start, previousStart, start - 1); break;
            case ConsoleKey.DownArrow:
                if (end == _length) break;
                var nextEnd = text.IndexOf('\n', end + 1); if (nextEnd < 0) nextEnd = _length;
                MoveLine(boundaries, start, end + 1, nextEnd); break;
        }
    }
    private void MoveLine(int[] boundaries, int start, int targetStart, int targetEnd)
    {
        var column = boundaries.Count(b => b >= start && b < Cursor);
        Cursor = boundaries.Where(b => b >= targetStart && b <= targetEnd).Skip(column).FirstOrDefault(targetEnd);
    }
    internal void Delete(bool before)
    {
        FinishCharacter(); if (Invalid || before && Cursor == 0 || !before && Cursor == _length) return;
        var boundaries = Boundaries();
        var start = before ? boundaries.Last(b => b < Cursor) : Cursor;
        var end = before ? Cursor : boundaries.First(b => b > Cursor);
        Array.Copy(_buffer, end, _buffer, start, _length - end);
        Array.Clear(_buffer, _length - (end - start), end - start);
        _length -= end - start; Cursor = start; SnapCursor();
    }
    internal void Load(string text, int cursor)
    {
        Array.Clear(_buffer); text.AsSpan().CopyTo(_buffer); _length = text.Length; Cursor = cursor; _pending = null;
    }
    internal void Clear() { FinishCharacter(); Load("", 0); }
    internal ChatDraftSnapshot Snapshot() => new(Text, Cursor);
    public void Dispose() { Array.Clear(_buffer); _length = 0; Cursor = 0; _pending = null; }
    public override string ToString() => "[ChatDraft]";
}

internal sealed class ChatDraftSnapshot(string text, int cursor)
{
    internal string Text { get; } = text;
    internal int Cursor { get; } = cursor;
    public override string ToString() => "[ChatDraftSnapshot]";
}