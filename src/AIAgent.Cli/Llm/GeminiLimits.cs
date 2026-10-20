namespace AIAgent.Cli.Llm;

internal static class GeminiLimits
{
    internal const int RequestTimeoutSeconds = 120;
    internal const int MaxOutputTokens = 8192;
    internal const int CandidateCount = 1;
    internal const int MaxRequestBodyBytes = 262144;
    internal const int MaxErrorBodyBytes = 65536;
    internal const string BaseUrl = "https://generativelanguage.googleapis.com";
}
