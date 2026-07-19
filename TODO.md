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

## Roadmap — planned (Tier 3/4/5)

The following are captured for later:

### Tier 3 — UX / productivity
- Working-changes **folder tree view** (directory tree instead of a flat file list).
- **Branch management panel** — local/remote/folder-style grouping, sorting, stale-branch cleanup,
  per-branch ahead/behind badges.
- **Image diff** (before/after side-by-side, slider).
- **Commit-message link detection** — `#123` / URLs become clickable (open issue/PR in browser).
- **Multi-repository tabs / workspace** — work on several repos at once.
- **Diff options** — ignore-whitespace toggle, moved-code detection.
- **Keyboard workflow** — customizable shortcuts, j/k graph navigation.

### Tier 4 — integration
- **Deeper GitHub/GitLab API** — PR/issue list + status, CI badges, review comments in-app
  (currently open-in-browser only).
- **OAuth sign-in** — GitHub device flow (currently manual PAT entry only).
- **External diff/merge tool** integration (e.g. Beyond Compare).
- **"Open in VS Code"** / editor integration.

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
