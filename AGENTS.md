# Project workflow

## Current phase — 2026-10-24

- Implementation resumed by explicit user instruction: “可以，做成可执行的计划按之前的流程给其他对话执行吧”. The approved baseline includes two actives, seven passives, a five-stage run, four standalone practice levels, and both retry-current-level and restart-run. Follow `Docs/RoguelikeDemoExecutionPlan.md` and `Docs/CombatWhitepaperSnapshot.md`; the previous requirements-only pause is lifted.
- Preserve delivered `demo-v0.1` and repository history. Project manager chat `01a16ac7-c334-7993-a5a3-82a5836f95ab` owns task dispatch, shared contracts, integration, Git, Unity processes, validation and delivery. Design chat `01a16a3d-b490-7cd3-bfb6-50264b2ce62e` owns design explanations and the battle whitepaper; it must not become a second implementation dispatcher.
- The source planning document `1TGUVyc6nMf5nF9yWr3aoB7BZZwU_YUb2lbiOyc84U0E` is strictly read-only: no content, formatting or comment writes. Design supplements belong only in the authorized battle whitepaper `1tCo9qlx488RquAvKMm83VkMvxZtScB7Zm0NyBGrwm3Y`. Historical v0.1 exclusions do not override the new approved scope.

## Engineering workflow

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
- Keep `main` at validated milestones. Save incomplete work on `codex/` branches with an explicit WIP commit and validation status. Push milestones to the configured `origin`, made public by explicit user instruction on 2026-10-24 ("改为公开仓库，任何人可查看"); do not force-push or rewrite shared history.
- The default Editor executable is `D:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe`; the script accepts `-EditorPath` for other installations.
