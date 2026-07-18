# TODO / Known Limitations

Honest record of what is not implemented, stubbed, or deferred.

## Implemented (for reference)
Repository open (folder / drag-drop / recent / reopen-last), color commit graph (virtualized),
commit details + colored unified diff, staging (stage/unstage/discard/commit/amend), branches
(checkout/create/delete/rename/merge/rebase), remotes (fetch/pull/push + ahead/behind via git CLI
with credential-helper delegation), commit search, light/dark theme, ko/en runtime language toggle,
beginner tooltips, TortoiseGit-style right-click context menus, `.gitignore` generator, GitHub
release-based auto-update.

## Not implemented / deferred
- **Merge/rebase conflict resolution UI** — the CLI operation runs and git's output is surfaced; on
  conflict the repository is left in the conflicted state for the user to resolve with their tools.
  No in-app 3-way merge editor, and no "abort" button yet.
- **Interactive rebase** (`rebase -i`) — not supported (the CLI's interactive editor isn't wired).
- **Stash** — not implemented.
- **Blame** — not implemented (listed in the spec's read set but not in the feature list).
- **Remote branch deletion** from the UI (`push --delete`) — only local branch deletion is exposed.
- **Side-by-side diff** — diff is unified only.
- **Submodules / worktrees / LFS** — no special handling.

## Notes / known behavior
- Screenshots in `docs/screenshots/` were captured with the app's `--topmost` flag because Visual
  Studio held the foreground on the dev machine; the flag is a legitimate feature (kiosk/screenshots).
- Push authentication is delegated to the system git credential helper (Git Credential Manager). If
  no helper is configured, git fails fast (`GIT_TERMINAL_PROMPT=0`) and the error is shown — the app
  does not hang or crash.
- The in-app updater only finds an update when a GitHub Release exists with an installer asset whose
  name ends in `.exe` (the release workflow produces `GitTab-Setup-<version>.exe`).
