namespace AIAgent.Cli.Core;

internal sealed class GenerationBudget(object? decisionGate = null)
{
    private readonly object _gate = decisionGate ?? new();
    private int _turnLimit = 5;
    private int _programLimit = 100;
    private int _programStarted;
    private int _turnStarted;
    private int _compactions;
    private long _revision;
    private long? _warnedRevision;
    internal void BeginConversation() { lock (_gate) { _turnStarted = 0; _compactions = 0; } }
    internal GenerationBudgetSnapshot Snapshot()
    { lock (_gate) return new(_turnLimit, _programLimit, _programStarted, _turnStarted, _compactions, _revision); }
    internal bool TryBeginGeneration(GenerationPurpose purpose, bool maintenance, CancellationToken token, Func<bool> current)
    {
        lock (_gate)
        {
            if (token.IsCancellationRequested || !current() || _programStarted >= _programLimit || !maintenance && _turnStarted >= _turnLimit) return false;
            _programStarted++;
            if (!maintenance) { _turnStarted++; if (purpose == GenerationPurpose.Compaction) _compactions++; }
            return true;
        }
    }
    internal bool SetTurnLimit(int value) => Set(value, true);
    internal bool SetProgramLimit(int value) => Set(value, false);
    private bool Set(int value, bool turn)
    {
        lock (_gate)
        {
            if (value < 1 || value > (turn ? 20 : 1000)) return false;
            if (value == (turn ? _turnLimit : _programLimit)) return true;
            var revision = checked(_revision + 1);
            if (turn) _turnLimit = value; else _programLimit = value;
            _revision = revision; return true;
        }
    }
    internal void RestoreDefaults()
    {
        lock (_gate)
        {
            if (_turnLimit == 5 && _programLimit == 100) return;
            var revision = checked(_revision + 1);
            _turnLimit = 5; _programLimit = 100; _revision = revision;
        }
    }
    internal bool TakeNearLimitWarning()
    {
        lock (_gate)
        {
            if (_programStarted * 5 < _programLimit * 4 || _warnedRevision == _revision) return false;
            _warnedRevision = _revision; return true;
        }
    }
    public override string ToString() => "[GenerationBudget]";
}

internal sealed class GenerationBudgetSnapshot(int turnLimit, int programLimit, int programStarted, int turnStarted,
    int compactions, long revision)
{
    internal int TurnLimit { get; } = turnLimit;
    internal int ProgramLimit { get; } = programLimit;
    internal int ProgramGenerationsStarted { get; } = programStarted;
    internal int TurnGenerationsStarted { get; } = turnStarted;
    internal int CompactionGenerationsStarted { get; } = compactions;
    internal int ProgramRemaining => Math.Max(0, ProgramLimit - ProgramGenerationsStarted);
    internal int TurnRemaining => Math.Max(0, TurnLimit - TurnGenerationsStarted);
    internal long BudgetRevision { get; } = revision;
    public override string ToString() => "[GenerationBudgetSnapshot]";
}