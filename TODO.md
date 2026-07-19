# TODO / Known Limitations

Honest record of what is implemented vs. deferred.

## Implemented
Repository open (folder / drag-drop / recent / reopen-last), color commit graph (custom-rendered,
virtualized), commit details + colored **unified and side-by-side** diff, staging
(stage/unstage/discard/commit/amend), branches (checkout/create/delete/rename/merge/rebase +
**remote-branch delete**), **tags** (create/delete/push), remotes (fetch/pull/push + ahead/behind,
credentials via git helper), commit search, light/dark theme + ko/en language **persisted across
restarts**, beginner tooltips, TortoiseGit-style right-click context menus, `.gitignore` generator,
**stash** (push/apply/pop/drop), **blame** view, **conflict handling** (in-progress banner with
Abort/Continue, stage-to-resolve), **interactive rebase** (pick/squash/fixup/drop + reorder),
**submodule update**, keyboard shortcuts (F5/Ctrl+R refresh, Ctrl+O open, Ctrl+Enter commit),
GitHub release-based auto-update.

## Deferred / partial (honest)
- **Conflict resolver** — there is an in-app resolver (per-block *Use ours / Use theirs / Both*,
  right-click a conflicted file → Resolve conflict) plus the Abort/Continue banner. It is **not** a
  full 3-way editor with the common ancestor pane or free-form hunk editing; complex conflicts may
  still be easier to resolve in a dedicated merge tool.

## Shipped in 1.2.0 (Tier 1/2)

Graph search/filter, diff syntax highlighting, commit/tag signing + verify badge, credential
management UI, worktrees, Git LFS, deeper submodules, patch import/export, line-level staging,
richer stash (diff preview + stash-to-branch), sparse-checkout, and blobless partial clone.

## Shipped in 1.3.0 (Tier 3)

Multi-repository tabs, folder-grouped changed files, image diff, clickable `#123`/URL links in
commit messages, ignore-whitespace diff toggle, merged-branch pruning (local/remote grouping and
ahead/behind badges were already present), and more keyboard shortcuts. **Follow-up known work:** the
tabs use a per-tab reopen on the existing view-model; the fuller app-shell / `RepositorySessionViewModel`
split (P0.1) is still worth doing to give each tab fully independent live state. Also deferred from
Tier 3: a true nested folder *tree* (current grouping is one level of folder headers), diff
moved-code detection, and customizable keybindings.

## Shipped in 1.4.0 (Tier 4)

In-app GitHub/GitLab pull-request & issue lists, per-commit CI status badge, "open in editor"
(VS Code, repo- and file-level) and an external-diff-tool action (`git difftool`). **OAuth device
sign-in** is implemented but **inert until a GitHub OAuth App client_id is registered** (set
`GITTAB_GITHUB_CLIENT_ID` or the `ClientId` constant in `GitHubDeviceFlow.cs`); PAT auth is used
otherwise. Still deferred from Tier 4: in-app PR **review comments** and merge tool beyond
`git difftool`/the built-in resolver.

## Shipped in 1.5.0 (convenience & management)

Force push (with lease), sign-off (`-s`) & co-author trailers, edit-commit-author, restore-file-to-a-
commit-version, a repository-statistics dashboard, a Git config editor, and "add to .gitignore".
**Deferred (still worth doing):** diff "expand context" lines, CHANGELOG generation from Conventional
Commits, and commit bookmarks.

## Roadmap — planned (Tier 5)

The following are captured for later:

### Tier 5 — distribution / accessibility / quality
- **winget / Microsoft Store** distribution + portable (no-install) zip.
- **Update channels** (stable/beta) + delta updates.
- **Accessibility** — item-level AutomationPeer, graph alt-text, high-contrast theme, font scaling.
- **More locales** — Japanese / Chinese / Spanish (i18n infra already in place).
- **Periodic background fetch** + "N commits behind" notification.
- **UI-automation / integration test** expansion.

## Notes
- Screenshots in `docs/screenshots/` were captured with the `--topmost` flag (a real feature) because
  Visual Studio held the foreground on the dev machine.
- Push auth is delegated to the system git credential helper; with none configured git fails fast
  (`GIT_TERMINAL_PROMPT=0`) and the error is surfaced — the app never hangs or crashes.
