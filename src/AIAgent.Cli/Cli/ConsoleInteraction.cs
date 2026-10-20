using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using System.Text;

namespace AIAgent.Cli.Cli;

internal sealed class ConsoleInteraction(ITerminal terminal) : IBootstrapInteraction, ITerminalCapability
{
    internal ISensitiveDataPolicy? Policy { private get; set; }
    public void EnsureAvailable()
    {
        try
        {
            if (!terminal.IsInteractive) throw new BootstrapFailureException(BootstrapErrorCode.TerminalUnavailable);
            using var probe = terminal.BeginInput();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        { throw new BootstrapFailureException(BootstrapErrorCode.TerminalUnavailable); }
    }

    public ValueTask<bool> ConfirmWorkspaceAsync(ConfirmedWorkspace workspace, PublicSettings settings, CancellationToken cancellationToken)
    {
        using var input = terminal.BeginInput();
        ShowStatus("工作區：" + workspace.RootPath);
        ShowStatus("模型：" + (settings.Model ?? "尚未設定模型"));
        ShowStatus("本機聊天與選取的檔案內容可能傳送至 Gemini；本階段尚未提供檔案工具");
        terminal.Write("使用此工作區？[y/N] ");
        var answer = new StringBuilder();
        var invalid = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = terminal.ReadKey(cancellationToken);
            if (key is null || HiddenKeyReader.IsControl(key.Value, ConsoleKey.D, '\u0004')) throw new BootstrapEndOfInputException();
            if (HiddenKeyReader.IsControl(key.Value, ConsoleKey.C, '\u0003')) throw new OperationCanceledException(cancellationToken);
            if (key.Value.Key == ConsoleKey.Enter || key.Value.KeyChar is '\r' or '\n')
            {
                terminal.Write(Environment.NewLine);
                var value = answer.ToString().Trim();
                return ValueTask.FromResult(!invalid && (value.Equals("y", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase)));
            }
            if (key.Value.Key is ConsoleKey.Backspace or ConsoleKey.Delete || key.Value.KeyChar is '\b' or '\u007f')
            {
                if (answer.Length > 0) { answer.Length--; terminal.Write("\b \b"); }
                continue;
            }
            if (HiddenKeyReader.IsNavigation(key.Value.Key)) continue;
            if (char.IsControl(key.Value.KeyChar)) { invalid = true; continue; }
            if (answer.Length >= 512) { invalid = true; continue; }
            answer.Append(key.Value.KeyChar);
            terminal.Write(key.Value.KeyChar.ToString());
        }
    }
    public ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken cancellationToken) => new(new HiddenKeyReader(terminal, Policy).Read(cancellationToken));
    public void ShowStatus(string safeMessage) => terminal.Write((Policy?.RedactForDisplay(safeMessage) ?? safeMessage) + Environment.NewLine);
}
