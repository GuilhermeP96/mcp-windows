using Sbroenne.WindowsMcp.Models;
using Sbroenne.WindowsMcp.Native;
using Sbroenne.WindowsMcp.Native.Tools;

namespace Sbroenne.WindowsMcp.Tests.Unit;

public sealed class WinAppCliServiceTests
{
    [Fact]
    public void BuildArguments_PreservesValuesAsSeparateProcessArguments()
    {
        var arguments = WinAppCliService.BuildArguments(
            WinAppUiAction.SetValue,
            "Notepad & calc.exe",
            null,
            "Text Area; exit 9",
            "hello | whoami",
            null,
            5000,
            4,
            50,
            false,
            false,
            false,
            false,
            false,
            null,
            false,
            false);

        Assert.Equal("ui", arguments[0]);
        Assert.Equal("set-value", arguments[1]);
        Assert.Contains("Notepad & calc.exe", arguments);
        Assert.Contains("Text Area; exit 9", arguments);
        Assert.Contains("hello | whoami", arguments);
        Assert.DoesNotContain("cmd.exe", arguments);
        Assert.DoesNotContain("powershell.exe", arguments);
    }

    [Fact]
    public void BuildArguments_WaitForMapsTypedOptions()
    {
        var arguments = WinAppCliService.BuildArguments(
            WinAppUiAction.WaitFor,
            null,
            "12345",
            "Result Display",
            "Complete",
            "Name",
            9000,
            4,
            50,
            true,
            true,
            false,
            false,
            false,
            null,
            false,
            false);

        Assert.Equal(
            ["ui", "wait-for", "Result Display", "--window", "12345", "--json",
             "--timeout", "9000", "--property", "Name", "--value", "Complete", "--gone", "--contains"],
            arguments);
    }

    [Fact]
    public void WireName_UsesStableSnakeCaseActionNames()
    {
        Assert.Equal("list_windows", WinAppCliService.WireName(WinAppUiAction.ListWindows));
        Assert.Equal("scroll_into_view", WinAppCliService.WireName(WinAppUiAction.ScrollIntoView));
    }

    [Theory]
    [InlineData(WinAppUiAction.Invoke)]
    [InlineData(WinAppUiAction.GetValue)]
    [InlineData(WinAppUiAction.SetValue)]
    [InlineData(WinAppUiAction.WaitFor)]
    [InlineData(WinAppUiAction.Search)]
    public void Validate_TargetedActionWithoutSelectorFails(WinAppUiAction action)
    {
        var error = WinAppUiTool.Validate(action, "notepad", null, null, "value", 5000, 4, 50);

        Assert.Contains("selector is required", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ListWindowsAllowsNoTarget()
    {
        Assert.Null(WinAppUiTool.Validate(
            WinAppUiAction.ListWindows, null, null, null, null, 5000, 4, 50));
    }

    [Fact]
    public void ResolveExecutable_UsesExplicitExistingPath()
    {
        var executable = Environment.ProcessPath!;
        var service = new WinAppCliService(executable);

        Assert.Equal(Path.GetFullPath(executable), service.ResolveExecutable());
    }
}
