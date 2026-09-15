using System.Diagnostics;
using System.Text.Json;

namespace Sbroenne.WindowsMcp.Native;

/// <summary>
/// Executes the first-party Microsoft winapp CLI without a command shell.
/// </summary>
public sealed class WinAppCliService
{
    private const string ExecutableVariable = "WINDOWS_MCP_WINAPP_PATH";
    private const string WorkflowVariable = "WINDOWS_MCP_WINAPP_WORKFLOW_ID";
    private readonly string? _configuredExecutable;

    /// <summary>Creates a service using automatic executable discovery.</summary>
    public WinAppCliService() : this(null)
    {
    }

    internal WinAppCliService(string? executablePath)
    {
        _configuredExecutable = executablePath;
    }

    /// <summary>Returns the detected executable path, or null when winapp is unavailable.</summary>
    public string? ResolveExecutable()
    {
        if (!string.IsNullOrWhiteSpace(_configuredExecutable))
        {
            return File.Exists(_configuredExecutable) ? Path.GetFullPath(_configuredExecutable) : null;
        }

        var configured = Environment.GetEnvironmentVariable(ExecutableVariable);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        var fromPath = FindOnPath("winapp.exe");
        if (fromPath is not null)
        {
            return fromPath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var portableRoot = Path.Combine(localAppData, "winappcli");
        if (!Directory.Exists(portableRoot))
        {
            return null;
        }

        return Directory.EnumerateFiles(portableRoot, "winapp.exe", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .OrderByDescending(path => ParseVersion(Path.GetFileName(Path.GetDirectoryName(path))))
            .FirstOrDefault();
    }

    /// <summary>Executes a supported UI action and captures its JSON response.</summary>
    public async Task<WinAppCliExecutionResult> ExecuteAsync(
        WinAppUiAction action,
        string? app,
        string? windowHandle,
        string? selector,
        string? value,
        string? property,
        int timeoutMs,
        int depth,
        int maxResults,
        bool gone,
        bool contains,
        bool interactive,
        bool hideDisabled,
        bool hideOffscreen,
        string? outputPath,
        bool captureScreen,
        bool focus,
        CancellationToken cancellationToken)
    {
        var executable = ResolveExecutable();
        if (executable is null)
        {
            return WinAppCliExecutionResult.Failure(
                action, "Microsoft winapp CLI was not found. Set WINDOWS_MCP_WINAPP_PATH or install Microsoft.WinAppCLI.");
        }

        var arguments = BuildArguments(action, app, windowHandle, selector, value, property,
            timeoutMs, depth, maxResults, gone, contains, interactive, hideDisabled, hideOffscreen,
            outputPath, captureScreen, focus);
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Corporate/default-safe behavior: do not emit Microsoft CLI telemetry from MCP calls.
        startInfo.Environment["WINAPP_CLI_TELEMETRY_OPTOUT"] = "1";
        var workflowId = Environment.GetEnvironmentVariable(WorkflowVariable);
        if (!string.IsNullOrWhiteSpace(workflowId))
        {
            startInfo.Environment["WINAPP_UI_WORKFLOW_ID"] = workflowId;
        }

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!process.Start())
            {
                return WinAppCliExecutionResult.Failure(action, "Microsoft winapp CLI failed to start.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // winapp startup and desktop arbitration can take materially longer than the UI wait
            // requested by the caller, especially on first invocation or a busy desktop.
            timeout.CancelAfter(Math.Clamp(timeoutMs + 30000, 30000, 150000));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                throw;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            stopwatch.Stop();
            return process.ExitCode == 0
                ? WinAppCliExecutionResult.SuccessResult(action, stdout, executable, stopwatch.ElapsedMilliseconds)
                : WinAppCliExecutionResult.Failure(action,
                    string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim(),
                    process.ExitCode, executable, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WinAppCliExecutionResult.Failure(action, $"Microsoft winapp CLI timed out after {timeoutMs + 30000} ms.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return WinAppCliExecutionResult.Failure(action, ex.Message);
        }
    }

    internal static IReadOnlyList<string> BuildArguments(
        WinAppUiAction action,
        string? app,
        string? windowHandle,
        string? selector,
        string? value,
        string? property,
        int timeoutMs,
        int depth,
        int maxResults,
        bool gone,
        bool contains,
        bool interactive,
        bool hideDisabled,
        bool hideOffscreen,
        string? outputPath,
        bool captureScreen,
        bool focus)
    {
        var arguments = new List<string> { "ui", Command(action) };
        if (!string.IsNullOrWhiteSpace(selector))
        {
            arguments.Add(selector);
        }
        if (action == WinAppUiAction.SetValue && value is not null)
        {
            arguments.Add(value);
        }
        if (!string.IsNullOrWhiteSpace(windowHandle))
        {
            arguments.Add("--window");
            arguments.Add(windowHandle);
        }
        else if (!string.IsNullOrWhiteSpace(app))
        {
            arguments.Add("--app");
            arguments.Add(app);
        }

        arguments.Add("--json");
        if (action == WinAppUiAction.Inspect)
        {
            arguments.Add("--depth");
            arguments.Add(depth.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AddFlag(arguments, interactive, "--interactive");
            AddFlag(arguments, hideDisabled, "--hide-disabled");
            AddFlag(arguments, hideOffscreen, "--hide-offscreen");
        }
        if (action == WinAppUiAction.Search)
        {
            arguments.Add("--max");
            arguments.Add(maxResults.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (action == WinAppUiAction.GetProperty && !string.IsNullOrWhiteSpace(property))
        {
            arguments.Add("--property");
            arguments.Add(property);
        }
        if (action == WinAppUiAction.WaitFor)
        {
            arguments.Add("--timeout");
            arguments.Add(timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AddOption(arguments, "--property", property);
            AddOption(arguments, "--value", value);
            AddFlag(arguments, gone, "--gone");
            AddFlag(arguments, contains, "--contains");
        }
        if (action == WinAppUiAction.Screenshot)
        {
            AddOption(arguments, "--output", outputPath);
            AddFlag(arguments, captureScreen, "--capture-screen");
            AddFlag(arguments, focus, "--focus");
        }
        return arguments;
    }

    private static string Command(WinAppUiAction action) => action switch
    {
        WinAppUiAction.GetProperty => "get-property",
        WinAppUiAction.GetValue => "get-value",
        WinAppUiAction.SetValue => "set-value",
        WinAppUiAction.ScrollIntoView => "scroll-into-view",
        WinAppUiAction.WaitFor => "wait-for",
        WinAppUiAction.ListWindows => "list-windows",
        WinAppUiAction.GetFocused => "get-focused",
        _ => action.ToString().ToLowerInvariant(),
    };

    internal static string WireName(WinAppUiAction action) => Command(action).Replace('-', '_');

    private static void AddOption(List<string> arguments, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments.Add(name);
            arguments.Add(value);
        }
    }

    private static void AddFlag(List<string> arguments, bool enabled, string name)
    {
        if (enabled)
        {
            arguments.Add(name);
        }
    }

    private static string? FindOnPath(string fileName)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Ignore malformed PATH entries and continue deterministic discovery.
            }
        }
        return null;
    }

    private static Version ParseVersion(string? directoryName)
    {
        var candidate = directoryName?.TrimStart('v', 'V');
        return Version.TryParse(candidate, out var version) ? version : new Version(0, 0);
    }
}

/// <summary>Structured result from Microsoft's winapp CLI.</summary>
public sealed record WinAppCliExecutionResult
{
    /// <summary>Whether winapp completed successfully.</summary>
    public required bool Success { get; init; }
    /// <summary>The requested winapp UI action.</summary>
    public required string Action { get; init; }
    /// <summary>Identifies the first-party execution backend.</summary>
    public string Backend { get; init; } = "microsoft-winappcli";
    /// <summary>Native process exit code, when the process started.</summary>
    public int? ExitCode { get; init; }
    /// <summary>Parsed JSON emitted by winapp.</summary>
    public JsonElement? Data { get; init; }
    /// <summary>Non-JSON stdout emitted by winapp.</summary>
    public string? Text { get; init; }
    /// <summary>Error returned by winapp or the adapter.</summary>
    public string? Error { get; init; }
    /// <summary>Resolved executable path, included only with diagnostics.</summary>
    public string? Executable { get; init; }
    /// <summary>Elapsed child-process time, included only with diagnostics.</summary>
    public long? DurationMs { get; init; }

    internal static WinAppCliExecutionResult SuccessResult(
        WinAppUiAction action, string stdout, string executable, long durationMs)
    {
        JsonElement? data = null;
        string? text = null;
        try
        {
            using var document = JsonDocument.Parse(stdout);
            data = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            text = stdout.Trim();
        }
        return new()
        {
            Success = true,
            Action = WinAppCliService.WireName(action),
            ExitCode = 0,
            Data = data,
            Text = text,
            Executable = executable,
            DurationMs = durationMs,
        };
    }

    internal static WinAppCliExecutionResult Failure(
        WinAppUiAction action, string error, int? exitCode = null,
        string? executable = null, long? durationMs = null) =>
        new()
        {
            Success = false,
            Action = WinAppCliService.WireName(action),
            ExitCode = exitCode,
            Error = error,
            Executable = executable,
            DurationMs = durationMs,
        };
}
