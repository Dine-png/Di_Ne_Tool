# Avatar tool reference restoration regression

Run `./Tests/AvatarToolRestoration/Run-RestorationRegression.ps1` with Unity
2022.3.22f1 (`-UnityEditorPath` overrides the installed editor path).
The runner copies the production auto-apply source into an isolated project
under `.codex_tmp/AvatarToolRestoration`.

Uses real Unity assets, serialization, scene objects, GlobalObjectId and
SessionState. Covers original FX/menu/parameter references and layer flags,
empty slots, distinct dresser bindings, missing originals, destroyed clones,
empty/partial caches across reload, disk manifest recovery, stale play-exit
guards, and build/upload success/error events after builder replacement.
SDK builder events and the three tool generators are stand-ins. Lifecycle
callbacks are invoked directly in the focused regression suite. Add `-PlaySmoke`
to test actual play enter/exit with all four combinations of domain/scene reload
enabled and disabled. This does not test real SDK uploads.
