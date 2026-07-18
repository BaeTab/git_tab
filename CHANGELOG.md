# Changelog

All notable changes to **Git Tab** are documented here. This project follows
[Semantic Versioning](https://semver.org/).

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

[0.6.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.6.0
[0.5.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.5.0
[0.4.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.4.0
[0.3.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.3.0
[0.2.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.2.0
[0.1.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.1.0
