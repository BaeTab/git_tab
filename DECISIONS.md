# DECISIONS

Running log of design/implementation decisions. One line each. Newest at top.

- **Product name = "Git Tab"** (display), `GitTab` for identifiers/assembly (exe `GitTab.exe`), matching the `git_tab` repo. Namespaces `GitTab.*` replace the spec's placeholder `GitClient.*` (structure/intent identical: Core/Graph never reference WPF). _(Renamed from the earlier working title "Braid" at the user's request.)_
- **Reopen last repository on startup** (or a repo path passed as a CLI arg) so the app resumes where the user left off — also enables an "Open in Git Tab" shell action.
- **Right-click context menus (TortoiseGit-style)** on commits (checkout/branch/tag/reset/revert/cherry-pick/copy), branches (checkout/merge/rebase/rename/delete), and files (stage/unstage/discard).
- **`GIT_EDITOR=:` default in the CLI runner** so windowless git never hangs on an interactive editor; `--continue`/merge keep their default messages. Interactive rebase overrides `GIT_SEQUENCE_EDITOR=cp <plan>` per-command to inject the todo (git runs it via its bundled sh). Validated by integration tests.
- **Conflicts** are surfaced via `GetState()` (LibGit2Sharp `CurrentOperation` + index conflicts) with a banner offering Abort/Continue; resolving = stage the file. No in-app 3-way editor (deferred, see TODO).
- **Interactive rebase** excludes `reword` (can't edit messages non-interactively); supports pick/squash/fixup/drop + reorder.
- **Settings** (theme + language) persisted to `%AppData%/GitTab/settings.json`, restored on startup.
- **Side-by-side diff** built from parsed hunks (removed→left, added→right, blank fillers keep rows aligned), with synced scrolling; toggled from the diff toolbar.
- **Target `net8.0` / `net8.0-windows`** per fixed stack. Built with .NET SDK 9 (backward-compatible); .NET 8 Desktop Runtime 8.0.29 is installed locally so the app builds *and* runs.
- **FluentAssertions pinned to 6.12.1** (Apache-2.0). v8+ moved to a commercial license — forbidden by the OSS-only redistribution rule.
- **LibGit2Sharp 0.31.0** for all read paths (log/branch/status/diff). MIT (libgit2: GPLv2 + linking exception) — redistributable.
- **Write/network operations shell out to `git.exe`** (commit/stage/checkout/branch/merge/rebase/fetch/pull/push) for auth + stability, hidden behind `IGitCommandRunner`.
- **Graph project is fully standalone** — defines its own `GraphCommit` input record instead of depending on Core, so the layout engine is unit-testable in isolation with zero git dependency.
- **Custom `CommitGraphControl : FrameworkElement` with `OnRender`** (not XAML DataTemplate) + manual viewport virtualization, per the perf requirement.
- **Credentials delegated to git credential helper** — no in-app credential UI (security + license risk avoidance).
- **Auto-update via GitHub Releases** — app checks the `latest` release, compares SemVer, downloads the InnoSetup installer asset, and launches it. No custom update server.
- **H.Soft / Hyun-woo Bae branding retained** — this is the personal `github.com/BaeTab` repo, not the company GitLab, so personal branding is permitted.
- **Runtime i18n (ko/en)** via a `LocalizationService` singleton exposing a bindable `this[key]` indexer; XAML binds `{Binding [Key]}` so toggling language updates all strings live without restart. Strings live in ko/en dictionaries.
- **Beginner-friendly UX** — every Git action carries a plain-language tooltip (what it does + when to use it, aimed at first-time Git users), plus guided empty-states. Both languages.
