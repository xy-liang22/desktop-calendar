# Desktop Calendar — Agent Instructions

These instructions apply to all coding agents working in this repository.

## 1. Project and source of truth

- Repository: `D:\projects\desktop-calendar` on native Windows. Do not use WSL.
- Product: Windows 10/11 desktop calendar, task planner, actual activity timeline, and weekly review; local-first with Chinese/English UI.
- Read `docs/PRD.md`, `docs/DECISIONS.md`, and relevant `docs/TESTING.md` sections before changing a feature.
- Treat the PRD as the functional specification, DECISIONS as approved technical/product choices, and this file as the workflow contract. Do not silently override a conflict. Ask the user if a material conflict cannot be resolved.
- Preserve existing working features and user data. Make focused, incremental changes; do not rewrite unrelated modules.

## 2. Stage scope and autonomy

- Work only within the currently approved stage or substage. Do not start the next stage without explicit user approval.
- Within the approved scope, autonomously inspect code, implement, refactor minimally, test, fix issues, document, and commit/push as authorized.
- Ask questions only when a material ambiguity affects expected behavior, safety, substantial design, compatibility, or irreversible actions. Make reasonable low-risk engineering decisions independently and record meaningful ones.
- A stage may contain multiple local iterations and commits; do not interrupt the user for routine checks.
- Do not claim an unimplemented feature is complete or a test has passed when it was not executed.

## 3. Verification: agent-first, user-last

- You own routine verification. Prefer unit, integration, regression, build, and end-to-end tests; use available computer-use/UI tools to perform real clicking, typing, dragging, scrolling, resizing, switching modes, closing/restarting, and persistence tests.
- For visible UI outcomes, inspect actual rendered output or screenshots when feasible. Compilation, HWND state, and off-screen rendering do not prove on-screen correctness.
- If UI automation cannot select a desktop-owned window, seek safe alternative evidence; distinguish observed behavior from inference. Do not silently fall back to declaring success.
- Request human validation only for genuinely unobservable environment-specific behavior, subjective appearance, required permissions, or consequential product choices. Consolidate unavoidable checks into at most 1–3 focused actions when possible.
- Never repeatedly ask the user to recheck scenarios already satisfactorily verified unless a change plausibly affects them.
- Mark test outcomes explicitly as `PASS`, `FAIL`, or `NOT VERIFIED`, and state evidence/limitations. Keep detailed logs in project docs or ignored artifacts, not in chat.
- Test changed behavior plus relevant regression risks before reporting completion. Never conceal known failures.

## 4. Desktop behavior and constraints

- The approved direction is a top-level WPF window with Explorer desktop owner, not `SetParent`/`WS_CHILD` embedding into Explorer; retain the verified transparent rounded UI.
- Desired default behavior: absent from taskbar and Alt+Tab, remains visible after Win+D, ordinary apps can cover it, and it remains interactive. Always-on-top is user-selectable.
- A tray entry is acceptable and may remain enabled for recovery and exit; hiding it is optional, never at the expense of reliable recovery.
- Preserve safe fallback, exit, and recovery paths. Do not use focus theft, busy loops, or continual Z-order coercion to simulate desktop behavior.
- Current verified behavior is not a universal compatibility guarantee; distinguish Windows version, multi-monitor, DPI, Explorer restart, and remote-session testing.
- Full/compact state, location, size, selected display, and desktop/topmost preference should restore reliably where the environment permits; handle disconnected monitors safely.
- Todo display invariant: incomplete items precede completed items within each sibling group; checking/unchecking must not destroy original ordering keys. Scheduled times are visually prominent, including in compact view.

## 5. Data, safety, and dependencies

- User content must remain local by default; no authentication or network sync of private planner data without explicit instruction. Calendar reference-data updates may use approved manual network access.
- Do not commit personal planner JSON, images, backups, secrets, credentials, machine-specific files, caches, binaries, or build artifacts. Review `git status` and staged paths before commits.
- Preserve schema compatibility and data safety; when real data is implemented, use safe writes, migrations, and recoverable backups in accordance with the PRD.
- Do not install packages, VS extensions, SDKs, or tools without prior approval. Explain why a dependency is needed before requesting approval.
- Do not modify Windows system-wide settings, registry, Explorer internals, startup entries, firewall, or elevated locations without explicit approval. Do not forcibly restart Explorer or change monitor/DPI settings on the user's behalf.
- Do not manipulate unrelated processes or files. Use only authorized project paths and explicitly approved user-level integration paths.

## 6. Git and documentation

- Work on the approved development branch (`dev/p1.1` for the current effort); do not modify, merge into, or push `main` without explicit approval.
- Make focused commits after meaningful verified work and push to the configured remote when authorized. No force push, history rewrite, branch deletion, or destructive cleanup without approval.
- If authentication or push fails, report the blocker; do not invent success or request passwords/tokens in chat.
- Keep `README.md`, `docs/PRD.md`, `docs/DECISIONS.md`, and `docs/TESTING.md` consistent with material changes. Distinguish Implemented, Planned, and Known Limitations.
- Record precise repro steps and outcomes for important failures. Avoid large generated reports or redundant documentation.

## 7. Communication

- Use concise Chinese in user-facing progress and completion reports, normally within 200 Chinese characters unless a substantive decision requires explanation.
- At stage completion say: (1) what changed, (2) what you actually verified, (3) outstanding FAIL/NOT VERIFIED items, (4) whether user action is needed and the smallest exact steps, (5) commit/push status.
- Do not ask the user to manually test a feature that can reasonably be verified through your tools.
- Stop after the approved stage/substage; await explicit approval before advancing.
- If blocked, state the concrete blocker, what was attempted, and one or two actionable options. Do not fabricate a completion claim.
