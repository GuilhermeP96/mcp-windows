# Microsoft winapp native backend

## Decision

Keep the built-in Windows MCP engine as the default and expose Microsoft `winapp CLI` as an
optional first-party lane through `winapp_ui`.

Both implementations use Windows UI Automation. `winapp` is not a replacement for UIA or a new
AI model; it is Microsoft's public-preview CLI over native Windows capabilities. The hybrid design
avoids replacing a mature, token-optimized MCP surface with a slower preview dependency while
making first-party capabilities available when they are materially better.

## Routing

Use built-in tools for:

- compact `ui_snapshot` and precise `ui_find` discovery;
- session-bound observed element IDs and post-action snapshots;
- structured table extraction, article reading, and OCR fallback;
- batches, macros, clipboard/process/window operations, and file dialogs;
- screenshot-plus-mouse/keyboard fallback for custom controls.

Use `winapp_ui` for:

- UIA pattern operations where foreground input should be avoided (`invoke`, `set_value`,
  `focus`, `scroll_into_view`, and property waits);
- first-party accessibility-tree comparison and Windows app testing;
- Windows Graphics Capture screenshots;
- cooperative desktop workflow arbitration supplied by Microsoft;
- differential diagnosis when the built-in engine and Windows platform disagree.

Do not silently retry a failed mutating action through the other backend. An uncertain retry can
double-submit, double-toggle, or type twice. Rediscover state and select the fallback explicitly.

## Local benchmark

Measured on 2026-09-15 against Windows build 26100 and modern Notepad, three depth-5 inspections:

| Engine | Median latency | Median response |
|---|---:|---:|
| Windows MCP 1.3.24 `ui_snapshot` | 422 ms | 3.6 KB |
| Microsoft winapp 0.6.0 `ui inspect --json` | 1,205 ms | 57.9 KB |

This benchmark validates the default routing decision; it is not a universal performance claim.
Repeat it on Electron, WinUI, WPF, WinForms, and Win32 fixtures before changing defaults.

## Installation and discovery

The adapter searches in this order:

1. `WINDOWS_MCP_WINAPP_PATH`;
2. `winapp.exe` on `PATH`;
3. versioned portable directories under `%LOCALAPPDATA%\winappcli`.

The child process is started directly with `ProcessStartInfo.ArgumentList`, never through `cmd.exe`
or PowerShell. This prevents selector/value text from becoming shell syntax. The adapter always
sets `WINAPP_CLI_TELEMETRY_OPTOUT=1` for child calls. Set
`WINDOWS_MCP_WINAPP_WORKFLOW_ID` to forward a stable, non-secret workflow identifier to Microsoft's
`WINAPP_UI_WORKFLOW_ID` arbitration mechanism.

Microsoft documents WinApp CLI as public preview. Keep it optional until compatibility, response
shape, latency, and reliability pass the repository's deterministic and LLM behavioral suites.

## References

- [Microsoft winapp CLI](https://github.com/microsoft/winappCli)
- [Microsoft UI Automation documentation](https://learn.microsoft.com/windows/apps/dev-tools/winapp-cli/ui-automation)
- [AI-assisted testing for Windows apps](https://learn.microsoft.com/windows/apps/develop/ai-assisted/testing)
