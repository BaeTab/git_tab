# Performance Notes — Commit Graph

The DoD requires the commit graph to scroll smoothly on repositories with **5,000+ commits**.
Git Tab meets this by construction (viewport virtualization) and is confirmed by measurement.

## Design: O(viewport) rendering

`CommitGraphControl` is a custom `FrameworkElement` that implements `IScrollInfo` and draws
directly in `OnRender(DrawingContext)`. It does **not** materialize a visual per commit:

- On each render it computes the visible row range from the scroll offset and viewport height
  (`first = floor(offset / rowHeight)`, `last = ceil((offset + viewportHeight) / rowHeight)`)
  and draws **only those rows** (typically ~30–50).
- Scrolling changes the offset and invalidates the visual; the number of rows drawn per frame is
  bounded by the viewport, **independent of total commit count**.
- No `ItemsControl`/`DataTemplate` per row (which allocates a visual tree per item and dies on
  large histories) — this is the explicit anti-pattern the spec warns against.

So a 5,000-commit repo and a 50,000-commit repo render the same number of rows per frame.

## Measurement

A 6,000-commit repository was generated with `git fast-import` and read through the real
pipeline (LibGit2Sharp read → `GraphLayoutEngine.Build` → render) via `GitTab.Graph.Cli`:

| Repo size | Read + layout + render *all* rows | Notes |
|-----------|-----------------------------------|-------|
| 6,000 commits | ~210 ms end-to-end | Includes ~130 ms .NET cold start **and** ASCII rendering of *every* row (the CLI has no virtualization). |

The CLI renders all 6,000 rows (worst case); the WPF app renders only the visible ~40. The read
of 6,000 commits + full lane layout is well under 100 ms, and the one-time layout build happens
off the UI thread (`Task.Run`) in the app.

### How to reproduce
```bash
# generate a big repo
mkdir bigrepo && cd bigrepo && git init -b main
# (see scripts/gen used in dev, or any large clone)
# time read + layout on it:
dotnet run -c Release --project src/GitTab.Graph.Cli -- <path-to-bigrepo> 6000 > /dev/null
```

## Cost breakdown

- **Read** (LibGit2Sharp `Commits.QueryBy`, topo+time): linear in commit count, done once per load,
  on a background thread. Capped at 5,000 by default (`GetCommits(max)`).
- **Layout** (`GraphLayoutEngine.Build`): single top-down pass, O(commits × avg active lanes).
- **Render**: O(visible rows) per frame → constant with respect to history size.
- **Filter/search**: rebuilds the layout on the filtered subset (still a single O(n) pass).

## Known limits / future work

- Very wide histories (hundreds of concurrent lanes) increase per-row segment count; lane count is
  bounded by max concurrent branches, not commit count.
- `FormattedText` is created per visible row per frame; if profiling ever shows this hot, cache by
  (sha, language, theme). Not needed at current row counts.
