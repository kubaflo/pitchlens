using System.ComponentModel;
using System.Text.RegularExpressions;

namespace PitchLens.Generation;

public sealed record CopilotDiagnostic(string Message, string Trace);

public static partial class CopilotDiagnostics
{
    public static CopilotDiagnostic Describe(string stage, Exception error)
    {
        var chain = new List<Exception>();
        void Add(Exception e)
        {
            if (chain.Count >= 8 || chain.Any(item => ReferenceEquals(item, e))) return;
            chain.Add(e);
            if (e is AggregateException aggregate) foreach (var inner in aggregate.InnerExceptions) Add(inner);
            else if (e.InnerException is { } inner) Add(inner);
        }
        Add(error);
        var reasons = chain.Select(e => Classify(e.Message)).Where(s => s is not null).Distinct().ToArray();
        var details = chain.Select(e =>
        {
            var type = e switch
            {
                IOException => "IOException",
                Win32Exception => "Win32Exception",
                UnauthorizedAccessException => "UnauthorizedAccessException",
                TimeoutException => "TimeoutException",
                InvalidOperationException => "InvalidOperationException",
                _ => "SDKException"
            };
            var exit = ExitCode().Match(e.Message);
            var code = exit.Success && int.TryParse(exit.Groups[1].Value, out var value) && value is >= -255 and <= 65535
                ? $", child exit={value}" : "";
            var operation = Operation().Match(e.Message);
            var area = e.Message.Contains("session-state", StringComparison.OrdinalIgnoreCase) ? ", area=copilot-session-state" :
                e.Message.Contains(".copilot", StringComparison.OrdinalIgnoreCase) ? ", area=copilot-local-data" : "";
            return $"{type}(HRESULT=0x{e.HResult:X8}{(e is Win32Exception native ? $", native={native.NativeErrorCode}" : "")}{code}" +
                $"{(operation.Success ? ", operation=" + operation.Groups[1].Value.ToLowerInvariant() : "")}{area})";
        });
        // Exception/stderr text may contain request bodies, paths or credentials. Log only allowlisted metadata.
        var phase = stage switch
        {
            "startup" => "runtime startup",
            "auth" => "authentication preflight",
            "models" => "model availability preflight",
            "session" => "session creation",
            "response" => "model response",
            "cleanup" => "runtime cleanup",
            _ => "connection setup"
        };
        var reason = reasons.Length == 0 ? "the SDK/runtime connection failed" : string.Join("; ", reasons);
        var advice = reasons.Any(s => s is "permission denied" or "sandbox restriction")
            ? "Check this app's sandbox and bundled-runtime access; a new token is not indicated."
            : reasons.Contains("native runtime load failure") ? "Check the bundled native runtime and its adjacent libraries."
            : "Run the no-AI connection check to distinguish packaged-runtime startup from login/model access.";
        return new($"Copilot failed during {phase}: {reason}. {advice}",
            $"PitchLens Copilot diagnostic | stage={phase} | {string.Join(" -> ", details)} | {reason}");
    }
    private static string? Classify(string message)
    {
        var text = message.ToLowerInvariant();
        if (text.Contains("sandbox")) return "sandbox restriction";
        if (text.Contains("permission denied") || text.Contains("operation not permitted") || text.Contains("eacces") || text.Contains("eperm"))
            return "permission denied";
        if (text.Contains("dlopen") || text.Contains("library not loaded") || text.Contains("failed to load") && text.Contains(".node"))
            return "native runtime load failure";
        if (text.Contains("no such file") || text.Contains("file not found")) return "runtime file missing";
        if (text.Contains("broken pipe")) return "broken runtime pipe";
        if (text.Contains("read-only file system")) return "runtime storage is read-only";
        if (text.Contains("connection") && (text.Contains("closed") || text.Contains("lost") || text.Contains("reset")) ||
            text.Contains("end of stream") || text.Contains("exited")) return "runtime transport closed";
        return null;
    }
    [GeneratedRegex(@"(?:exit(?:ed)?(?:\s+with)?(?:\s+code)?|exitcode)\s*[:=]?\s*(-?\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExitCode();
    [GeneratedRegex(@"\b(mkdir|open|unlink|rename|read|write|spawn|exec)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Operation();
}
