namespace AIAgent.Cli.Workspace;

internal sealed class WorkspaceFileMatch(string path)
{
    internal string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    public override string ToString() => "[WorkspaceFileMatch]";
}

internal sealed class WorkspaceContextLine(int lineNumber, string content)
{
    internal int LineNumber { get; } = lineNumber > 0 ? lineNumber : throw new ArgumentOutOfRangeException(nameof(lineNumber));
    internal string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));
    public override string ToString() => "[WorkspaceContextLine]";
}

internal sealed class WorkspaceTextMatch(string path, int lineNumber, string content,
    IReadOnlyList<WorkspaceContextLine> beforeLines, IReadOnlyList<WorkspaceContextLine> afterLines)
{
    internal string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    internal int LineNumber { get; } = lineNumber > 0 ? lineNumber : throw new ArgumentOutOfRangeException(nameof(lineNumber));
    internal string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));
    internal IReadOnlyList<WorkspaceContextLine> BeforeLines { get; } = Array.AsReadOnly(beforeLines.ToArray());
    internal IReadOnlyList<WorkspaceContextLine> AfterLines { get; } = Array.AsReadOnly(afterLines.ToArray());
    public override string ToString() => "[WorkspaceTextMatch]";
}

internal sealed class WorkspaceRangeReadData(string path, int? startLine, int? endLine, int totalLines,
    string content, long fileByteCount, long contentByteCount, bool hasUtf8Bom, bool hasMore)
{
    internal string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    internal int? StartLine { get; } = startLine;
    internal int? EndLine { get; } = endLine;
    internal int TotalLines { get; } = totalLines;
    internal string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));
    internal long FileByteCount { get; } = fileByteCount;
    internal long ContentByteCount { get; } = contentByteCount;
    internal bool HasUtf8Bom { get; } = hasUtf8Bom;
    internal bool HasMore { get; } = hasMore;
    public override string ToString() => "[WorkspaceRangeReadData]";
}

internal enum DiscoveryKind { FileSearch, TextSearch, LineRange }

internal sealed class WorkspaceDiscoverySummary
{
    internal DiscoveryKind Kind { get; }
    internal int ReturnedCount { get; }
    internal int? StartLine { get; }
    internal int? EndLine { get; }
    internal int? TotalLines { get; }
    internal long? ContentByteCount { get; }
    internal WorkspaceDiscoverySummary(DiscoveryKind kind, int returnedCount, int? startLine = null,
        int? endLine = null, int? totalLines = null, long? contentByteCount = null)
    {
        if (!Enum.IsDefined(kind) || returnedCount < 0 ||
            (kind != DiscoveryKind.LineRange && (startLine is not null || endLine is not null || totalLines is not null || contentByteCount is not null)) ||
            (kind == DiscoveryKind.LineRange && (totalLines is not >= 0 || contentByteCount is not >= 0 ||
                (totalLines == 0 ? startLine is not null || endLine is not null || returnedCount != 0 :
                    startLine is not > 0 || endLine < startLine || endLine > totalLines || endLine is null || returnedCount != (long)endLine - startLine + 1))))
            throw new ArgumentException("探索摘要無效");
        Kind = kind; ReturnedCount = returnedCount; StartLine = startLine; EndLine = endLine;
        TotalLines = totalLines; ContentByteCount = contentByteCount;
    }
    public override string ToString() => "[WorkspaceDiscoverySummary]";
}

internal sealed class WorkspaceFileSearchData(string path, IReadOnlyList<WorkspaceFileMatch> matches, bool truncated)
{
    internal string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    internal IReadOnlyList<WorkspaceFileMatch> Matches { get; } = Array.AsReadOnly(matches.ToArray());
    internal bool Truncated { get; } = truncated;
    internal bool SafetyFiltered => true;
    public override string ToString() => "[WorkspaceFileSearchData]";
}

internal sealed class WorkspaceTextSearchData(string path, IReadOnlyList<WorkspaceTextMatch> matches, bool truncated)
{
    internal string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    internal IReadOnlyList<WorkspaceTextMatch> Matches { get; } = Array.AsReadOnly(matches.ToArray());
    internal bool Truncated { get; } = truncated;
    internal bool SafetyFiltered => true;
    public override string ToString() => "[WorkspaceTextSearchData]";
}

internal sealed class WorkspaceFileSearchResult
{
    internal WorkspaceStatus Status { get; }
    internal WorkspaceErrorCode ErrorCode { get; }
    internal WorkspaceFileSearchData? Data { get; }
    private WorkspaceFileSearchResult(WorkspaceStatus status, WorkspaceErrorCode errorCode, WorkspaceFileSearchData? data)
    { Status = status; ErrorCode = errorCode; Data = data; }
    internal static WorkspaceFileSearchResult Success(WorkspaceFileSearchData data) => new(WorkspaceStatus.Success, WorkspaceErrorCode.None, data ?? throw new ArgumentNullException(nameof(data)));
    internal static WorkspaceFileSearchResult End(WorkspaceStatus status, WorkspaceErrorCode code)
    { WorkspaceResultRules.ValidateEnd(status, code); return new(status, code, null); }
    public override string ToString() => "[WorkspaceFileSearchResult]";
}

internal sealed class WorkspaceTextSearchResult
{
    internal WorkspaceStatus Status { get; }
    internal WorkspaceErrorCode ErrorCode { get; }
    internal WorkspaceTextSearchData? Data { get; }
    private WorkspaceTextSearchResult(WorkspaceStatus status, WorkspaceErrorCode errorCode, WorkspaceTextSearchData? data)
    { Status = status; ErrorCode = errorCode; Data = data; }
    internal static WorkspaceTextSearchResult Success(WorkspaceTextSearchData data) => new(WorkspaceStatus.Success, WorkspaceErrorCode.None, data ?? throw new ArgumentNullException(nameof(data)));
    internal static WorkspaceTextSearchResult End(WorkspaceStatus status, WorkspaceErrorCode code)
    { WorkspaceResultRules.ValidateEnd(status, code); return new(status, code, null); }
    public override string ToString() => "[WorkspaceTextSearchResult]";
}

internal sealed class WorkspaceRangeReadResult
{
    internal WorkspaceStatus Status { get; }
    internal WorkspaceErrorCode ErrorCode { get; }
    internal WorkspaceRangeReadData? Data { get; }
    private WorkspaceRangeReadResult(WorkspaceStatus status, WorkspaceErrorCode errorCode, WorkspaceRangeReadData? data)
    { Status = status; ErrorCode = errorCode; Data = data; }
    internal static WorkspaceRangeReadResult Success(WorkspaceRangeReadData data) => new(WorkspaceStatus.Success, WorkspaceErrorCode.None, data ?? throw new ArgumentNullException(nameof(data)));
    internal static WorkspaceRangeReadResult End(WorkspaceStatus status, WorkspaceErrorCode code)
    { WorkspaceResultRules.ValidateEnd(status, code); return new(status, code, null); }
    public override string ToString() => "[WorkspaceRangeReadResult]";
}