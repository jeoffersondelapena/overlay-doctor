# Overlay Doctor

One in-game command that brings the ACT overlay stack back: `/overlays fix`.

```
/overlays          how the IINACT parser and the Browsingway renderer are right now
/overlays fix      restart whichever layer is unwell, or load a plugin that is missing
```

It is a Dalamud plugin that depends on nothing but Dalamud, so it still exists when IINACT or
Browsingway failed to load. It talks to both over Dalamud's plugin-to-plugin channel (`IINACT.Healthy`,
`IINACT.Restart`, `Browsingway.Healthy`, `Browsingway.Restart`, provided by the macOS forks of those
plugins) and reaches for Dalamud's plugin manager only to load a plugin that is not running.

| File | What it is |
|---|---|
| `OverlayDoctor/Doctor.cs` | the decision: which steps, in which order (pure, tested) |
| `OverlayDoctor/PluginControl.cs` | load or reload another plugin through Dalamud's internals |
| `OverlayDoctor/Plugin.cs` | the command, the IPC calls, the chat lines |
| `OverlayDoctor.Tests/` | xunit; runs before every commit once the hook is enabled |

The IPC names above are a contract with the two forks (`iinact-fork`, `browsingway-fork`): change them in all
three places or not at all.

Build: `dotnet build OverlayDoctor -c Release` (needs a Dalamud dev install; on macOS XIV on Mac's).
The first build enables the versioned pre-commit hook (`git config core.hooksPath .githooks`).
