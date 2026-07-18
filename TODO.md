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
- **Visual 3-way merge editor** — conflicts are handled via the banner (Abort/Continue) and by
  staging resolved files, but there is no in-app side-by-side merge-conflict editor. You resolve
  conflicts in the working tree (or your editor of choice), stage, then Continue.
- **Interactive rebase "reword"** — excluded from the UI because the backend runs non-interactively
  (`GIT_EDITOR=:`), so commit messages can't be edited mid-rebase. Pick/Squash/Fixup/Drop + reorder
  are supported.
- **Bisect** — the in-progress state is detected and shown, but there's no bisect driver UI.
- **Submodules** — `submodule update --init --recursive` is exposed; add/remove/sync are not.
- **Git LFS / worktrees** — no special handling.

## Notes
- Screenshots in `docs/screenshots/` were captured with the `--topmost` flag (a real feature) because
  Visual Studio held the foreground on the dev machine.
- Push auth is delegated to the system git credential helper; with none configured git fails fast
  (`GIT_TERMINAL_PROMPT=0`) and the error is surfaced — the app never hangs or crashes.
