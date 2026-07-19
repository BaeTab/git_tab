# Changelog

All notable changes to **Git Tab** are documented here. This project follows
[Semantic Versioning](https://semver.org/).

## [1.2.0] - 2026-07-19

A large power-user batch, plus toolbar and theming polish.

### Added
- **Commit & tag signing** — sign commits (`-S`) and create signed/annotated tags with GPG or SSH;
  the commit details panel shows a **verified-signature badge**.
- **Diff syntax highlighting** — code in the diff view is colored per language (by file extension),
  on top of the existing line and word-level highlighting.
- **Saved-credentials manager** — view and delete the PATs stored in Windows Credential Manager.
- **Worktrees** — list, add, remove, and prune linked working trees.
- **Git LFS** — track/untrack patterns, pull, and see tracked-file status.
- **Submodules (richer)** — add, sync, deinit, and per-submodule status (beyond update).
- **Patch import/export** — export a commit as a `.patch` (format-patch) and apply patches
  (`git apply` or `git am`).
- **Line-level staging** — stage only the checked lines of a hunk (GUI `git add -p`).
- **Stash (richer)** — preview a stash's diff and turn a stash into a branch.
- **Sparse checkout** — set/list/disable checkout patterns, plus **blobless partial clone**
  (`--filter=blob:none`) for large repositories.

### Changed
- **Toolbar** no longer scrolls horizontally — secondary/management actions collapse into a single
  overflow (**≡**) menu.
- The **commit-type combo box** is now themed to match light/dark.

### Engineering
- Regression suite grown to 123 tests (line-level patch builder, worktrees, signing config,
  stash, sparse-checkout, and more). Core service split into a partial file for readability.

## [1.1.0] - 2026-07-18

Power-user history editing and sharper diffs.

### Added
- **Reword commit** — change any commit's message from its right-click menu. HEAD is amended;
  older commits are rewritten via an automatic interactive rebase (nothing else in the history
  changes).
- **Bisect** — hunt a bad commit by binary search. Start from a commit's menu (mark it good, HEAD
  bad), then mark each checked-out commit Good / Bad / Skip from the on-screen bisect banner until
  git pinpoints the first bad commit; Reset ends the session.
- **Word-level diff highlighting** — within a modified line, only the words that actually changed
  are tinted (a stronger shade over the row), in both unified and split views, so edits are far
  easier to spot.

### Engineering
- Regression suite grown to 109 tests (added intra-line word-diff, reword, and bisect coverage).

## [1.0.0] - 2026-07-18

First stable release. Focused on stability, cancellation, and test confidence on top of the
full feature set from 0.x.

### Added
- **Cancellation** — Fetch/Pull/Push/Clone/Stash can be cancelled (a Cancel button in the status
  bar and in the standalone operation dialog); the underlying git process is stopped.

### Hardened
- **Large diffs are skipped** rather than parsed/rendered (avoids freezing on huge or generated
  files); binary and too-large changes show a clear placeholder.
- Empty / unborn (no-commit) repositories are handled without errors.

### Engineering
- Regression suite grown to 101 tests, including a localization-completeness guard (Korean and
  English key sets must match) and ViewModel orchestration tests (NSubstitute).

## [0.6.1] - 2026-07-18

### Fixed
- Toolbar: the right-hand controls (language / theme / update / settings) were cut off at narrow /
  non-maximized window widths. The management buttons now scroll horizontally while the network
  actions, search, and right-hand controls stay pinned and visible.

## [0.6.0] - 2026-07-18

### Added
- **Clone** — clone a remote repository from a URL, with an auto-derived folder name and GUI
  authentication. Also available from the Explorer right-click menu.
- **File history** — the commits that touched a file, with the file's diff at each commit (from a
  file's right-click menu).
- **Compare** — diff two branches, tags, or commits: changed files and per-file diffs.
- **Content search (pickaxe)** — find commits that added or removed a string (or regex) in the code,
  and jump to them.
- **Command palette** — `Ctrl+P` opens a searchable list of every action.
- **Explorer menu expanded** to 8 items (adds **Clone** and **Stash changes**).

### Security / hardening
- **Argument-injection hardening** — ref/branch/tag/remote names are validated so a name starting
  with `-` can't be misread by git as an option.
- **Update integrity** — releases publish a SHA-256, and the auto-updater verifies the downloaded
  installer before launching it.

### Engineering
- CI: a `dotnet format` gate, CodeQL security analysis, and Dependabot. MinGit pinned for
  reproducible installers. Regression suite grown to 92 tests (NSubstitute added for ViewModel tests).

## [0.5.0] - 2026-07-18

### Added
- **Create repository** — turn a folder into a new repo (`git init`), optionally wiring up an
  `origin` remote, from the toolbar or welcome screen.
- **Remotes manager** — add, change the URL of, or remove remotes.
- **History & undo (reflog)** — browse recent HEAD positions and restore to any of them, with a
  one-click "undo last action" — a safety net for mistaken commits/merges/resets.
- **Partial staging** — stage individual hunks of a file (the GUI equivalent of `git add -p`) via
  a per-hunk "stage" action.
- **Visual 3-way merge editor** — resolve conflicts with base / ours / theirs reference panes and an
  editable result, plus "use ours / use theirs / keep both" quick actions.
- **Commit message helper** — a Conventional-Commits type dropdown (feat/fix/docs/…) that prefixes
  the message.
- **GitHub/GitLab integration** — "New PR" opens the pre-filled Pull/Merge Request page for the
  current branch, and "Open on web" opens the repository page (no token required).
- **Crash reports (opt-in)** — write a local crash report on unexpected errors, plus an "open logs
  folder" action, from Settings › Diagnostics.
- **Accessibility** — screen-reader names on toolbar actions and a UI Automation peer for the commit
  graph that announces the selected commit; keyboard navigation throughout.

### Changed
- Large repositories load incrementally: the first page of commits loads instantly and more load as
  you scroll, preserving your scroll position.
- Added a `--dark` command-line flag (mirrors `--light`).

### Testing
- Regression suite expanded to 68 tests (init, remotes, reflog, credential-key parsing, auth-failure
  detection, remote-URL/PR-URL parsing).

## [0.4.0] - 2026-07-18

### Added
- **Windows Explorer integration** — a TortoiseGit-style right-click menu (**Open / Commit /
  Pull / Push / Fetch / History**) on any folder and folder background. Pull/Push/Fetch/Commit
  open in a dedicated standalone dialog; Open/History launch the main window. Registered under
  `HKCU` (no admin) via `ExtendedSubCommandsKey`, and installed automatically by the setup
  (togglable in **Settings**).
- **GUI authentication** — push/pull to private HTTPS remotes with no console setup. On an auth
  failure you're prompted for a username + Personal Access Token, which is stored securely in the
  **Windows Credential Manager** and reused automatically. Works even with no credential helper,
  via a built-in `GIT_ASKPASS` provider.
- **Bundled Git** — the installer ships portable Git (MinGit), so Git Tab is a self-contained
  all-in-one tool that works without a separate Git install. A system Git is auto-detected if present.
- **Settings window** — theme, language, Explorer-integration toggle, detected Git path, "clear
  saved credentials", and version/about, opened from the toolbar gear.
- **Standalone operation dialogs** — Pull/Push/Fetch show a progress + result window; Commit opens
  a focused staging dialog.

### Changed
- Custom commit-graph and header now repaint immediately on theme switch.
- Title bar shows only the app name (repository name removed).
- README screenshots refreshed against a multi-branch demo repository; added a beginner **Wiki**.

### Fixed
- Light theme leaving the commit graph rendered with dark colors after a runtime theme switch.
- Explorer submenu appearing empty on Windows 11 (switched to `ExtendedSubCommandsKey`).

## [0.3.0] - 2026-07-18
- GitKraken/GitLens-style columnar graph, author avatars, changes bars, custom title bar and
  scrollbars, themed context menus, responsive columns, larger side-by-side diff.

## [0.2.0] - 2026-07-18
- Conflict banner (abort/continue), stash, blame, remote-branch delete, tags, interactive rebase,
  submodule update, side-by-side diff, settings persistence, keyboard shortcuts.

## [0.1.0] - 2026-07-18
- Initial release: color commit graph, commit details + diff, staging & commit, branches & tags,
  fetch/pull/push, `.gitignore` generator, light/dark themes, Korean/English UI, auto-update.

[1.0.0]: https://github.com/BaeTab/git_tab/releases/tag/v1.0.0
[0.6.1]: https://github.com/BaeTab/git_tab/releases/tag/v0.6.1
[0.6.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.6.0
[0.5.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.5.0
[0.4.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.4.0
[0.3.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.3.0
[0.2.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.2.0
[0.1.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.1.0
