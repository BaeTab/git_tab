# Manual QA Log

Verification performed against the real `git_tab` repository (and synthetic repos) on Windows 11,
.NET 8 Desktop Runtime, built with SDK 9.

| # | Scenario | Result | Evidence |
|---|----------|--------|----------|
| 1 | App launches, DI + Serilog init, no crash | ✅ | Log `GitTab starting up` + window shown |
| 2 | Open repository (CLI arg / recent / drag-drop) | ✅ | Log `Opened repository …`; graph populates |
| 3 | Color commit graph renders (nodes, lanes, chips, HEAD ring) | ✅ | `docs/screenshots/main-dark.png`, `main-light.png` |
| 4 | Graph shape matches `git log --graph` (branch + merge + parallel) | ✅ | `docs/graph-ascii-sample.txt` |
| 5 | Select commit → details (message/author/date/parents) + changed files | ✅ | Screenshots (right panel) |
| 6 | Click changed file → colored unified diff (add/del/hunk backgrounds) | ✅ | Screenshot (build.yml diff, green +lines) |
| 7 | Working copy status: staged / unstaged / untracked lists | ✅ | Screenshot (left panel with real changes) |
| 8 | Light ⇄ dark theme toggle | ✅ | Both theme screenshots |
| 9 | Korean UI + ko/en runtime toggle | ✅ | Screenshots in Korean; toggle button present |
| 10 | Beginner tooltips on actions | ✅ | Tooltip style + `.Tip` strings wired to every action |
| 11 | Right-click context menus (commit / branch / file) | ✅ | ContextMenus wired to commands (checkout/branch/tag/reset/revert/cherry-pick/merge/rebase/…) |
| 12 | `.gitignore` generator (stack detection + write/append) | ✅ | Toolbar button + unit tests (`GitignoreServiceTests`) |
| 13 | 6,000-commit repo read + layout | ✅ (~210 ms incl. startup) | `docs/perf-notes.md` |
| 14 | App icon in title bar / taskbar | ✅ | `docs/screenshots/*` (GT mark in title bar) |
| 15 | Global exception handler keeps app alive | ✅ | `DispatcherUnhandledException` handled + logged |

## v0.2.0 additions
| # | Scenario | Result | Evidence |
|---|----------|--------|----------|
| 16 | Interactive rebase (squash) via `GIT_SEQUENCE_EDITOR` | ✅ | integration test `Interactive_rebase_squash_reduces_commit_count` |
| 17 | Merge conflict detected + aborted | ✅ | integration test `Merge_conflict_is_detected_then_aborted` + conflict banner |
| 18 | Stash push → pop roundtrip | ✅ | integration test `Stash_push_then_pop_roundtrips_changes` |
| 19 | Blame per-line authorship | ✅ | integration test `Blame_attributes_each_line_to_its_commit` + Blame view |
| 20 | Tags render as chips; tag/remote-branch delete + stash sections | ✅ | screenshot (v0.9 chip), Branches tab wiring |
| 21 | Side-by-side diff toggle + synced scroll | ✅ | screenshot (Split toggle), DiffView split build |
| 22 | Settings (theme/language) persisted across restarts | ✅ | `%AppData%/GitTab/settings.json` |
| 23 | Release-based update detects newer version | ✅ | v0.1.0 → v0.2.0 published; updater compares SemVer |

## Automated
- `dotnet build GitTab.sln` → 0 warnings, 0 errors.
- `dotnet test GitTab.sln` → **25 passing** (10 graph layout, 15 core: reads + diff parse + gitignore +
  rebase/conflict/stash/blame/range integration tests).
- **GitHub Actions `build` workflow green on `windows-latest`** (clean checkout → restore → build → test),
  and the `release` workflow publishes the InnoSetup installer to a GitHub Release.

## Exercised by build/logic but not driven end-to-end here (require credentials / conflicts)
- Actual `push` to a remote (needs configured credentials) — command wired; auth delegated to git.
- Merge/rebase **with conflicts** — command runs; git output surfaced; no in-app resolver (see TODO).
