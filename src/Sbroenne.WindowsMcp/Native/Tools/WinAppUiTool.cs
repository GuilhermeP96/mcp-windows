using System.ComponentModel;
using System.Runtime.Versioning;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sbroenne.WindowsMcp.Tools;

namespace Sbroenne.WindowsMcp.Native.Tools;

/// <summary>Optional first-party Windows UI automation lane backed by Microsoft winapp CLI.</summary>
[SupportedOSPlatform("windows")]
[McpServerToolType]
public static partial class WinAppUiTool
{
    /// <summary>
    /// Use Microsoft's optional winapp CLI for Windows-native UIA operations. Prefer existing ui_snapshot/ui_find/ui_read
    /// for compact discovery, tables, OCR, macros, and file dialogs. Use this tool when first-party winapp behavior is
    /// specifically valuable: UIA pattern actions without foreground input, cooperative desktop workflows, accessibility
    /// inspection, WGC screenshots, or comparison against Microsoft's implementation.
    /// Keywords: Microsoft winapp, native Windows automation, first-party UIA, accessibility tree, invoke pattern,
    /// set value without focus, WGC screenshot, Windows app testing.
    /// </summary>
    /// <param name="action">Native action: status, inspect, search, get_property, get_value, invoke, set_value, focus, scroll_into_view, wait_for, list_windows, get_focused, or screenshot.</param>
    /// <param name="app">Target process name, window title, or PID. Ignored when windowHandle is supplied.</param>
    /// <param name="windowHandle">Target HWND as a decimal string. Preferred after list_windows because it survives title changes.</param>
    /// <param name="selector">Microsoft winapp selector: AutomationId, semantic slug, or visible text. Required by targeted actions.</param>
    /// <param name="value">Value for set_value, or expected value for wait_for.</param>
    /// <param name="property">UIA property for get_property or wait_for.</param>
    /// <param name="timeoutMs">wait_for timeout in milliseconds. Child execution includes a separate 30-second startup and desktop-arbitration allowance.</param>
    /// <param name="depth">inspect tree depth, from 1 through 20.</param>
    /// <param name="maxResults">Maximum search results, from 1 through 500.</param>
    /// <param name="gone">For wait_for, wait until the selector disappears.</param>
    /// <param name="contains">For wait_for with value, use substring matching.</param>
    /// <param name="interactive">For inspect, return only interactive elements.</param>
    /// <param name="hideDisabled">For inspect, omit disabled elements.</param>
    /// <param name="hideOffscreen">For inspect, omit offscreen elements.</param>
    /// <param name="outputPath">For screenshot, optional PNG destination path.</param>
    /// <param name="captureScreen">For screenshot, capture screen pixels so popups and overlays are included.</param>
    /// <param name="focus">For screenshot, foreground the target before capture.</param>
    /// <param name="includeDiagnostics">Include executable path and elapsed time. Omitted by default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [McpServerTool(Name = "winapp_ui", Title = "Microsoft WinApp UI Automation", Destructive = true, OpenWorld = false)]
    public static async partial Task<CallToolResult> ExecuteAsync(
        WinAppUiAction action,
        [DefaultValue(null)] string? app,
        [DefaultValue(null)] string? windowHandle,
        [DefaultValue(null)] string? selector,
        [DefaultValue(null)] string? value,
        [DefaultValue(null)] string? property,
        [DefaultValue(5000)] int timeoutMs,
        [DefaultValue(4)] int depth,
        [DefaultValue(50)] int maxResults,
        [DefaultValue(false)] bool gone,
        [DefaultValue(false)] bool contains,
        [DefaultValue(false)] bool interactive,
        [DefaultValue(false)] bool hideDisabled,
        [DefaultValue(false)] bool hideOffscreen,
        [DefaultValue(null)] string? outputPath,
        [DefaultValue(false)] bool captureScreen,
        [DefaultValue(false)] bool focus,
        [DefaultValue(false)] bool includeDiagnostics,
        CancellationToken cancellationToken)
    {
        var validation = Validate(action, app, windowHandle, selector, value, timeoutMs, depth, maxResults);
        if (validation is not null)
        {
            return WindowsToolsBase.FailResult(validation);
        }

        var result = await WindowsToolsBase.WinAppCliService.ExecuteAsync(
            action, app, windowHandle, selector, value, property, timeoutMs, depth, maxResults,
            gone, contains, interactive, hideDisabled, hideOffscreen, outputPath, captureScreen, focus,
            cancellationToken);

        if (!includeDiagnostics)
        {
            result = result with { Executable = null, DurationMs = null };
        }
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, WindowsToolsBase.JsonOptions) }],
            IsError = !result.Success,
        };
    }

    internal static string? Validate(
        WinAppUiAction action, string? app, string? windowHandle, string? selector,
        string? value, int timeoutMs, int depth, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(app) && string.IsNullOrWhiteSpace(windowHandle)
            && action != WinAppUiAction.ListWindows)
        {
            return "Supply app or windowHandle for deterministic targeting.";
        }
        if (!string.IsNullOrWhiteSpace(windowHandle)
            && (!long.TryParse(windowHandle, out var handle) || handle <= 0))
        {
            return "windowHandle must be a positive decimal HWND.";
        }
        if (RequiresSelector(action) && string.IsNullOrWhiteSpace(selector))
        {
            return $"selector is required for {action}. Use inspect or search first.";
        }
        if (action == WinAppUiAction.SetValue && value is null)
        {
            return "value is required for set_value.";
        }
        if (timeoutMs is < 100 or > 120000)
        {
            return "timeoutMs must be between 100 and 120000.";
        }
        if (depth is < 1 or > 20)
        {
            return "depth must be between 1 and 20.";
        }
        if (maxResults is < 1 or > 500)
        {
            return "maxResults must be between 1 and 500.";
        }
        return null;
    }

    private static bool RequiresSelector(WinAppUiAction action) => action is
        WinAppUiAction.Search or WinAppUiAction.GetProperty or WinAppUiAction.GetValue or WinAppUiAction.Invoke or
        WinAppUiAction.SetValue or WinAppUiAction.Focus or WinAppUiAction.ScrollIntoView or
        WinAppUiAction.WaitFor;
}
