using System.Net;
using System.Reflection;
using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GeminiLifetimeTests
{
    [TestMethod]
    public async Task GEMAC021_PreCancelledRequestHasNoTransmission()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        await using var client = new GeminiClient(session, handler);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var result = await client.GenerateAsync(GeminiFixtures.Request(), cancellation.Token);
        Assert.IsTrue(result.Status == LlmStatus.Cancelled && result.ErrorCode == LlmErrorCode.None && !result.RequestDispatched && handler.Calls == 0, "GEM-AC-021");
    }

    [TestMethod]
    public async Task GEMAC008_ConcurrentCallIsRejectedAndCompletedSlotCanBeReused()
    {
        using var session = GeminiFixtures.Session(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new GeminiHandler { Respond = async (_, token) => { entered.TrySetResult(); await release.Task.WaitAsync(token); return GeminiHandler.Json(GeminiFixtures.Text()); } };
        await using var client = new GeminiClient(session, handler);
        var first = client.GenerateAsync(GeminiFixtures.Request(), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await client.GenerateAsync(GeminiFixtures.Request(), default);
        Assert.IsTrue(second.ErrorCode == LlmErrorCode.InvalidRequest && !second.RequestDispatched && handler.Calls == 1, "GEM-AC-008");
        release.TrySetResult();
        Assert.IsTrue((await first).Status == LlmStatus.Completed && (await client.GenerateAsync(GeminiFixtures.Request(), default)).Status == LlmStatus.Completed && handler.Calls == 2, "GEM-AC-008");
    }

    [TestMethod]
    public async Task GEMAC021_TimeoutAndCancellationStopLateSuccessWithExternalPriority()
    {
        foreach (var externalCancellation in new[] { false, true })
        {
            using var session = GeminiFixtures.Session(); var clock = new DeadlineClock();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new GeminiHandler { Respond = async (_, _) => { entered.TrySetResult(); await release.Task; return GeminiHandler.Json(GeminiFixtures.Text()); } };
            await using var client = new GeminiClient(session, handler, clock);
            using var cancellation = new CancellationTokenSource();
            var pending = client.GenerateAsync(GeminiFixtures.Request(), cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(clock.DueTime == TimeSpan.FromSeconds(120), "GEM-AC-005");
            var sdk = typeof(GeminiClient).GetField("sdk", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
            var api = sdk.GetType().GetField("_apiClient", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sdk)!;
            var options = (Google.GenAI.Types.HttpOptions)api.GetType().GetProperty("HttpOptions")!.GetValue(api)!;
            Assert.IsTrue(options.Timeout == 120000, "GEM-AC-005");
            clock.Fire();
            if (externalCancellation) cancellation.Cancel();
            release.TrySetResult();
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(result.RequestDispatched && result.ModelMessage is null && handler.Calls == 1, "GEM-AC-021");
            Assert.IsTrue(externalCancellation ? result.Status == LlmStatus.Cancelled && result.ErrorCode == LlmErrorCode.None : result.Status == LlmStatus.Failed && result.ErrorCode == LlmErrorCode.Timeout, "GEM-AC-021");
        }
    }

    [TestMethod]
    public async Task GEMAC021_ExternalCancellationDuringResponseReadIsPropagated()
    {
        using var session = GeminiFixtures.Session(); using var cancellation = new CancellationTokenSource();
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new WaitingContent(reading) }) };
        await using var client = new GeminiClient(session, handler);
        var pending = client.GenerateAsync(GeminiFixtures.Request(), cancellation.Token);
        await reading.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(result.Status == LlmStatus.Cancelled && result.RequestDispatched && handler.Calls == 1 && result.Usage is null, "GEM-AC-021");
    }

    [TestMethod]
    public async Task GEMAC022_DisposeCancelsAndWaitsWithoutOwningSession()
    {
        using var session = GeminiFixtures.Session(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new GeminiHandler { Respond = async (_, token) => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return GeminiHandler.Json(GeminiFixtures.Text()); } };
        var client = new GeminiClient(session, handler);
        var pending = client.GenerateAsync(GeminiFixtures.Request(), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var a = client.DisposeAsync().AsTask(); var b = client.DisposeAsync().AsTask();
        await Task.WhenAll(a,b).WaitAsync(TimeSpan.FromSeconds(5));
        var result = await pending;
        Assert.IsTrue(result.ErrorCode == LlmErrorCode.ClientUnavailable && result.RequestDispatched && handler.Disposed && session.Secrets.AuthenticationKey.Length > 0, "GEM-AC-022");
        Assert.IsTrue((await client.GenerateAsync(GeminiFixtures.Request(), default)).ErrorCode == LlmErrorCode.ClientUnavailable && handler.Calls == 1, "GEM-AC-022");
    }

    [TestMethod]
    public async Task GEMAC003_ProductHandlerDisablesRedirectAndGuardRejectsAlternateUris()
    {
        using var session = GeminiFixtures.Session();
        await using (var client = new GeminiClient(session))
        {
            var transport = (GeminiTransport)typeof(GeminiClient).GetField("transport", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
            Assert.IsTrue(transport.InnerHandler is SocketsHttpHandler { AllowAutoRedirect: false }, "GEM-AC-003");
        }
        foreach (var uri in new[] { "https://invalid.example/v1beta/models/gemini-3.5-flash-lite:generateContent", "http://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent", "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent?key=FAKE_ONLY" })
        {
            var handler = new GeminiHandler(); using var guard = new GeminiTransport(handler, "gemini-3.5-flash-lite", session.Policy) { Operation = new() };
            using var http = new HttpClient(guard); using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent("{}") };
            try { await http.SendAsync(request); Assert.Fail("GEM-AC-003"); }
            catch (GeminiBoundaryException ex) { Assert.IsTrue(ex.Code == LlmErrorCode.InvalidRequest && handler.Calls == 0, "GEM-AC-003"); }
        }
    }

    [TestMethod]
    public async Task GEMAC017_FinalSerializedBodyChecksDecodedValuesAndFieldNames()
    {
        using var session = GeminiFixtures.Session();
        foreach (var json in new[] { "{\"ordinary\":\"FAKE_KEY_FOR_TESTS_ONL\\u0059\"}", "{\"FAKE_KEY_FOR_TESTS_ONL\\u0059\":\"ordinary\"}" })
        {
            var handler = new GeminiHandler();
            using var guard = new GeminiTransport(handler, "gemini-3.5-flash-lite", session.Policy) { Operation = new() };
            using var http = new HttpClient(guard);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent") { Content = new StringContent(json) };
            try { await http.SendAsync(request); Assert.Fail("GEM-AC-017"); }
            catch (GeminiBoundaryException ex) { Assert.IsTrue(ex.Code == LlmErrorCode.SensitiveInput && !guard.Operation!.RequestDispatched && handler.Calls == 0, "GEM-AC-017"); }
        }
    }

    private sealed class WaitingContent(TaskCompletionSource reading) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token) { reading.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
    }
}

internal sealed class DeadlineClock : TimeProvider
{
    private DeadlineTimer? timer;
    internal TimeSpan DueTime { get; private set; }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    { DueTime = dueTime; return timer = new DeadlineTimer(callback, state); }
    internal void Fire() => timer!.Fire();
    private sealed class DeadlineTimer(TimerCallback callback, object? state) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        internal void Fire() => callback(state);
    }
}
