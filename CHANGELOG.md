# Changelog

All notable changes to **Git Tab** are documented here. This project follows
[Semantic Versioning](https://semver.org/).

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

[0.4.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.4.0
[0.3.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.3.0
[0.2.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.2.0
[0.1.0]: https://github.com/BaeTab/git_tab/releases/tag/v0.1.0
