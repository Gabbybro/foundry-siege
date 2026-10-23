# Project workflow

- Use Unity **6000.3.27f1** (revision `672d562a42a7`). Do not upgrade the Editor or dependencies without a concrete reason.
- Project root is this directory. The project uses C#, Unity 2D physics, and the Built-In Render Pipeline.
- The initial `InstallationCheck` scene is only a development environment check; it is not the finished game.
- Prefer Editor APIs to generate scenes/prefabs and bind serialized references. Preserve asset `.meta` files.
- Run `pwsh -File Tools/Unity.ps1 Setup` to configure the project and create the check scene if absent.
- Run `pwsh -File Tools/Unity.ps1 Validate` for scene/reference checks.
- Run `pwsh -File Tools/Unity.ps1 Build` then `pwsh -File Tools/Unity.ps1 Smoke` for a Windows build and an actual runtime physics/collision check.
- Close the Editor for this project before batch commands; do not run two Unity processes on one project.
- Logs, builds, package caches, and installers are ignored by Git. Never commit `.setup`, `Library`, `Logs`, or `Builds`.
- Git integration is owned by the project manager while execution chats share this checkout. Module chats must not switch branches, merge, reset, or push this shared repository.
- Keep `main` at validated milestones. Save incomplete work on `codex/` branches with an explicit WIP commit and validation status. Push milestones to the configured private `origin`; do not force-push or rewrite shared history.
- The default Editor executable is `D:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe`; the script accepts `-EditorPath` for other installations.
