using System.Text.Json.Serialization;

namespace Sbroenne.WindowsMcp.Models;

/// <summary>Actions delegated to Microsoft's optional winapp CLI.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WinAppUiAction>))]
public enum WinAppUiAction
{
    /// <summary>Show target connection information.</summary>
    [JsonStringEnumMemberName("status")]
    Status,
    /// <summary>Inspect the accessibility tree.</summary>
    [JsonStringEnumMemberName("inspect")]
    Inspect,
    /// <summary>Search the accessibility tree.</summary>
    [JsonStringEnumMemberName("search")]
    Search,
    /// <summary>Read one or all UIA properties.</summary>
    [JsonStringEnumMemberName("get_property")]
    GetProperty,
    /// <summary>Read a control value.</summary>
    [JsonStringEnumMemberName("get_value")]
    GetValue,
    /// <summary>Invoke the best supported UIA action pattern.</summary>
    [JsonStringEnumMemberName("invoke")]
    Invoke,
    /// <summary>Set a control value through UIA.</summary>
    [JsonStringEnumMemberName("set_value")]
    SetValue,
    /// <summary>Move keyboard focus through UIA.</summary>
    [JsonStringEnumMemberName("focus")]
    Focus,
    /// <summary>Scroll an element into view through UIA.</summary>
    [JsonStringEnumMemberName("scroll_into_view")]
    ScrollIntoView,
    /// <summary>Wait for an element or property condition.</summary>
    [JsonStringEnumMemberName("wait_for")]
    WaitFor,
    /// <summary>List visible application windows.</summary>
    [JsonStringEnumMemberName("list_windows")]
    ListWindows,
    /// <summary>Return the currently focused element.</summary>
    [JsonStringEnumMemberName("get_focused")]
    GetFocused,
    /// <summary>Capture a window or element through Windows Graphics Capture.</summary>
    [JsonStringEnumMemberName("screenshot")]
    Screenshot,
}
