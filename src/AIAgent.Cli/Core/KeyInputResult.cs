using AIAgent.Cli.Security;

namespace AIAgent.Cli.Core;

public sealed class KeyInputResult : IDisposable
{
    private char[]? key;
    public KeyInputStatus Status { get; }
    private KeyInputResult(KeyInputStatus status, char[]? ownedKey = null) { Status = status; key = ownedKey; }
    internal static KeyInputResult Accept(ReadOnlySpan<char> value) => new(KeyInputStatus.Accepted, value.ToArray());
    internal static KeyInputResult End(KeyInputStatus status) => status == KeyInputStatus.Accepted ? throw new ArgumentException("狀態無效") : new(status);
    internal SessionSecrets TakeSecrets()
    {
        if (Status != KeyInputStatus.Accepted || key is null) throw new InvalidOperationException("無可移交的認證資料");
        var secrets = new SessionSecrets(key);
        key = null;
        return secrets;
    }
    public void Dispose()
    {
        if (key is not null) { Array.Clear(key); key = null; }
    }
    public override string ToString() => "[KeyInputResult]";
}