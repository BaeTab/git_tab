# TODO / Known Limitations

Honest record of what's done, what's left, and what's intentionally limited. Current release: **v1.8.0**.

## ✅ Implemented (through v1.5.0)

- **Repository** — open (folder / drag-drop / recent / reopen-last), `git init`, **clone** (+ blobless
  partial clone), **multiple repository tabs** (each fully independent — its own live state & git service).
- **Commit graph** — custom-rendered, virtualized, color-coded lanes, avatars, change bars,
  incremental loading; text search/filter; per-commit signature & CI-status badges; **bookmarks**.
- **Diff** — unified & side-by-side, word-level highlighting, **syntax highlighting**, ignore-whitespace
  toggle, **expand/collapse context**, **image diff** (before/after); changed files as a **folder tree**;
  clickable `#123`/URL links.
- **Staging & commit** — stage/unstage/discard, **hunk & line-level** staging, commit/amend,
  **GPG/SSH signing**, **sign-off (-s)**, **co-author** trailer, **edit author**, Conventional-Commit
  type helper.
- **History editing** — interactive rebase (pick/squash/fixup/drop/reorder), **reword**, **bisect**,
  cherry-pick, revert, **restore file to a commit's version**, reflog undo.
- **Branches/tags** — checkout/create/delete/rename/merge/rebase, remote-branch delete,
  **prune merged**, ahead/behind badges; tags create/delete/push (+ signed/annotated).
- **Remotes & network** — fetch/pull/push, **force-push (with lease)**, cancellation; GUI auth (PAT in
  Windows Credential Manager) + **saved-credentials manager**.
- **Power tools** — worktrees, Git LFS, deeper submodules, patch import/export, sparse-checkout,
  richer stash (diff preview + stash-to-branch), blame, file history, compare, content search (pickaxe).
- **Hosting** — in-app GitHub/GitLab **PR & issue lists**, CI status, "open in editor" (VS Code) +
  external diff tool.
- **Management** — **repository statistics dashboard**, **Git config editor**, **CHANGELOG generator**,
  "add to .gitignore", `.gitignore` generator, 3-way conflict resolver + abort/continue banner.
- **App** — light/dark theme, ko/en (persisted), command palette (Ctrl+P), keyboard shortcuts,
  Explorer right-click integration + standalone dialogs, single-instance, crash-report opt-in,
  accessibility first pass, **periodic background fetch** (per-repo "behind" indicator),
  GitHub release-based auto-update (SHA-256 verified).

## 🔭 Remaining

### Tier 5 — distribution / accessibility / quality
- **winget / Microsoft Store** distribution + portable (no-install) zip.
- **Update channels** (stable/beta) + delta updates.
- **Accessibility** — item-level AutomationPeer, graph alt-text, high-contrast theme, font scaling.
- **More locales** — Japanese / Chinese / Spanish (i18n infra + a XAML-key coverage test are in place).

### Deferred features (worth doing, not tied to a tier)
- Diff **moved-code detection** (`--color-moved`). *(Expand/collapse context shipped in 1.6.0.)*
- **Customizable keybindings**.
- In-app **PR review comments** (currently PR/issue *lists* only).
- Full **3-way conflict editor** (common-ancestor pane / free-form hunk editing) — today's resolver is
  per-block *use ours / use theirs / both* plus stage-to-resolve.

### Blocked on external setup
- **OAuth device sign-in** — the flow is fully implemented but inert until a GitHub OAuth App
  **client_id** is registered (`GITTAB_GITHUB_CLIENT_ID`, or the `ClientId` const in
  `GitHubDeviceFlow.cs`). PAT auth is used otherwise.

## Notes
- **Code signing is intentionally excluded** (cost). Update integrity is instead guaranteed by the
  published SHA-256 that the updater verifies before launching an installer.
- The **auto-updater download** was fixed in **v1.4.2**: it used to hash the installer while the file
  was still open, breaking every real update. Builds **v1.4.1 and earlier** therefore need a one-time
  **manual** install of the latest release (see the pinned notice / README banner).
- Push auth is delegated to the system git credential helper; with none configured, git fails fast
  (`GIT_TERMINAL_PROMPT=0`) and the error is surfaced — the app never hangs.
- Screenshots in `docs/screenshots/` were captured with the `--topmost` flag (a real feature) because
  Visual Studio held the foreground on the dev machine.
