using System.Globalization;
using System.Text;

namespace AIAgent.Cli.Processes;

/// <summary>SDK 10 的單一完整整體摘要；不從模組成功文字推導測試結果。</summary>
internal sealed class MtpSummaryParser
{
    private readonly StringBuilder _line = new();
    private readonly Dictionary<string, long> _counts = new(StringComparer.Ordinal);
    private int _lineBytes;
    private int _summaryBytes;
    private int _candidates;
    private bool _invalid;
    private bool _longLine;
    private bool _duration;
    private bool _complete;
    internal TestCounts? Counts
    {
        get
        {
            if (!_complete || _invalid || _candidates != 1 || _counts.Count != 4) return null;
            try
            {
                var value = new TestCounts(_counts["total"], _counts["succeeded"], _counts["failed"], _counts["skipped"]);
                return value.Passed > 0 && value.Failed == 0 ? value : null;
            }
            catch (Exception ex) when (ex is ArgumentException or OverflowException) { return null; }
        }
    }
    internal void Feed(string text)
    {
        if (_complete) throw new InvalidOperationException("摘要已結束");
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                if (_longLine) { if (_candidates > 0) _invalid = true; }
                else Line(_line.ToString());
                _line.Clear(); _lineBytes = 0; _longLine = false;
            }
            else
            {
                _lineBytes += rune.Utf8SequenceLength;
                if (_lineBytes > 8192)
                {
                    if (!_longLine && _line.ToString().StartsWith("Test run summary:", StringComparison.Ordinal)) _invalid = true;
                    _longLine = true; _line.Clear();
                }
                else if (!_longLine) _line.Append(rune.ToString());
            }
        }
    }
    internal void Complete()
    {
        if (_longLine || _line.Length != 0 && (_candidates != 1 || _counts.Count != 4 || _line.ToString().Any(c => c is not (' ' or '\t')))) _invalid = true;
        _complete = true;
    }
    private void Line(string text)
    {
        if (text.StartsWith("Test run summary:", StringComparison.Ordinal))
        {
            _candidates++; _summaryBytes = Encoding.UTF8.GetByteCount(text) + 1;
            if (_candidates != 1 || text != "Test run summary: Passed!") _invalid = true;
            return;
        }
        if (_candidates == 0) return;
        _summaryBytes = checked(_summaryBytes + Encoding.UTF8.GetByteCount(text) + 1);
        if (_summaryBytes > 8192) { _invalid = true; return; }
        var trimmed = text.Trim(' ', '\t');
        if (trimmed.Length == 0) return;
        var colon = trimmed.IndexOf(':');
        if (colon <= 0) { _invalid = true; return; }
        var key = trimmed[..colon].TrimEnd(' ', '\t');
        var value = trimmed[(colon + 1)..].Trim(' ', '\t');
        if (key == "duration")
        {
            if (_duration || value.Length == 0 || _counts.Count != 4) _invalid = true;
            _duration = true; return;
        }
        if (key is not ("total" or "failed" or "succeeded" or "skipped") || _duration || value.Length == 0 ||
            value.Any(c => !char.IsAsciiDigit(c)) || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
            !_counts.TryAdd(key, count)) _invalid = true;
    }
    public override string ToString() => "[MtpSummaryParser]";
}