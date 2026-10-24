using System.Globalization;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Processes;

/// <summary>有界雙串流；原文只在目前操作的記憶體存在，絕不直接顯示。</summary>
internal sealed class ProcessOutputCollector(ISensitiveDataPolicy policy, Action<string> requestStop)
{
    private readonly object _gate = new();
    private readonly Channel _stdout = new();
    private readonly Channel _stderr = new();
    private readonly MtpSummaryParser _summary = new();
    private string _state = "safe";
    private bool _limit;
    private bool _signalled;
    internal string State { get { lock (_gate) return _state; } }
    internal bool OutputLimit { get { lock (_gate) return _limit; } }
    internal long StdoutBytes { get { lock (_gate) return _stdout.Bytes; } }
    internal long StderrBytes { get { lock (_gate) return _stderr.Bytes; } }
    internal bool StdoutTruncated { get { lock (_gate) return _stdout.Truncated; } }
    internal bool StderrTruncated { get { lock (_gate) return _stderr.Truncated; } }
    internal TestCounts? Tests { get { lock (_gate) return _state == "safe" && !_limit ? _summary.Counts : null; } }
    internal string Stdout { get { lock (_gate) return Text(_stdout); } }
    internal string Stderr { get { lock (_gate) return Text(_stderr); } }
    internal string Head(bool stderr)
    {
        lock (_gate) return _state == "safe" ? Escape((stderr ? _stderr : _stdout).Head.ToString()) : "";
    }
    internal string Tail(bool stderr)
    {
        lock (_gate) return _state == "safe" ? Escape((stderr ? _stderr : _stdout).Tail.ToString()) : "";
    }
    internal void Feed(bool stderr, ReadOnlySpan<byte> bytes)
    {
        lock (_gate)
        {
            var channel = stderr ? _stderr : _stdout;
            try
            {
                channel.Bytes = checked(channel.Bytes + bytes.Length);
                if (checked(_stdout.Bytes + _stderr.Bytes) > 1048576)
                { _limit = true; Stop("process_output_limit"); }
                if (_state != "safe" || _limit) return;
                var chars = new char[bytes.Length + 2];
                var count = channel.Decoder.GetChars(bytes, chars, false);
                Consume(channel, new string(chars, 0, count), stderr);
            }
            catch (DecoderFallbackException) { Fail("invalid_utf8", "process_output_invalid"); }
            catch (OverflowException) { Fail("unavailable", "process_io_failure"); }
        }
    }
    internal void Complete(bool stderr)
    {
        lock (_gate)
        {
            if (_state != "safe" || _limit) return;
            var channel = stderr ? _stderr : _stdout;
            try
            {
                var chars = new char[2];
                var count = channel.Decoder.GetChars([], chars, true);
                Consume(channel, new string(chars, 0, count), stderr);
                if (!stderr) _summary.Complete();
            }
            catch (DecoderFallbackException) { Fail("invalid_utf8", "process_output_invalid"); }
        }
    }
    internal async Task ReadAsync(Stream stream, bool stderr, CancellationToken token)
    {
        var buffer = new byte[4096];
        try
        {
            while (true)
            {
                var count = await stream.ReadAsync(buffer, token);
                if (count == 0) break;
                Feed(stderr, buffer.AsSpan(0, count));
            }
            Complete(stderr);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        { lock (_gate) Fail("unavailable", "process_io_failure"); }
    }
    private void Consume(Channel channel, string text, bool stderr)
    {
        if (!Safe(channel.RawCarry + text)) { Fail("sensitive", "sensitive_content"); return; }
        channel.RawCarry = Carry(channel.RawCarry + text);
        var visible = new StringBuilder();
        foreach (var c in text)
        {
            if (channel.Ansi == 1) { channel.Ansi = c == '[' ? 2 : c == ']' ? 3 : 0; continue; }
            if (channel.Ansi == 2) { if (c is >= '@' and <= '~') channel.Ansi = 0; continue; }
            if (channel.Ansi == 3) { if (c == '\a') channel.Ansi = 0; else if (c == '\u001b') channel.Ansi = 4; continue; }
            if (channel.Ansi == 4) { channel.Ansi = c == '\\' ? 0 : 3; continue; }
            if (c == '\u001b') { channel.Ansi = 1; continue; }
            if (!char.IsControl(c)) visible.Append(c);
        }
        var shown = channel.VisibleCarry + visible;
        if (!Safe(shown)) { Fail("sensitive", "sensitive_content"); return; }
        channel.VisibleCarry = Carry(shown);
        if (!stderr) _summary.Feed(text);
        foreach (var rune in text.EnumerateRunes()) channel.Retain(rune);
        if (!Safe(_stdout.Head + _stdout.Tail.ToString() + _stderr.Head + _stderr.Tail.ToString()))
            Fail("sensitive", "sensitive_content");
    }
    private bool Safe(string text) => policy.CanSendOrUse(text, ContentBoundary.ToolResult);
    // SessionSecrets accepts at most 512 ASCII key characters; retain every possible cross-block match.
    private static string Carry(string text) => text.Length <= 511 ? text : text[^511..];
    private string Text(Channel channel)
    {
        if (_state != "safe") return "";
        var raw = channel.Head + channel.Tail.ToString();
        var value = Escape(raw);
        if (Safe(value)) return value;
        Fail("sensitive", "sensitive_content"); return "";
    }
    private static string Escape(string raw)
    {
        var escaped = new StringBuilder();
        foreach (var c in raw)
            if (char.IsControl(c) && c is not ('\n' or '\t')) escaped.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            else escaped.Append(c);
        return escaped.ToString();
    }
    private void Fail(string state, string reason)
    {
        if (_state == "sensitive") return;
        _state = state; _stdout.Clear(); _stderr.Clear(); Stop(reason);
    }
    private void Stop(string reason) { if (!_signalled) { _signalled = true; requestStop(reason); } }
    private sealed class Channel
    {
        internal readonly Decoder Decoder = new UTF8Encoding(false, true).GetDecoder();
        internal readonly StringBuilder Head = new();
        internal readonly StringBuilder Tail = new();
        internal string RawCarry = "";
        internal string VisibleCarry = "";
        internal int Ansi;
        internal long Bytes;
        private int _headBytes;
        private int _tailBytes;
        private bool _headClosed;
        private bool _evicted;
        internal bool Truncated => Bytes > 16384 || _evicted;
        internal void Retain(Rune rune)
        {
            var bytes = rune.Utf8SequenceLength;
            if (!_headClosed && _headBytes + bytes <= 4096) { Head.Append(rune.ToString()); _headBytes += bytes; return; }
            _headClosed = true; Tail.Append(rune.ToString()); _tailBytes += bytes;
            while (_tailBytes > 12288)
            {
                _evicted = true;
                var length = char.IsHighSurrogate(Tail[0]) ? 2 : 1;
                var first = Tail.ToString(0, length); _tailBytes -= Encoding.UTF8.GetByteCount(first); Tail.Remove(0, length);
            }
        }
        internal void Clear() { Head.Clear(); Tail.Clear(); RawCarry = VisibleCarry = ""; }
    }
    public override string ToString() => "[ProcessOutputCollector]";
}