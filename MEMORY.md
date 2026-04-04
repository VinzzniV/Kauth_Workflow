# MEMORY.md

## Purpose

This file is short-lived working memory only.
It is not a backlog, not a changelog, and not a second project documentation file.

Use it only for:
- current focus
- active traps or local workflow issues
- 1-3 temporary findings that are likely useful in the next session

Do not let this file grow continuously.
If an entry is no longer useful soon, remove it.
If something became stable project truth, move it to the proper documentation instead of keeping it here.

---

## Current Focus

- The documented P0-P3 backlog is currently complete.
- Next work should start only from a newly defined task block, not by continuing old cleanup indefinitely.

## Active Risks / Watchouts

- A locally running `dotnet run` or `dotnet watch` can lock backend build artifacts and interfere with local backend test runs.
- The frontend build is green, but Vite still warns that the main chunk is above the 500 kB warning threshold. That is optimization territory, not an active blocker.

## Temporary Notes

- `TODO.md` is the authoritative backlog file. `MEMORY.md` should only keep short-lived session context.
- Recent frontend refactors are complete and verified with `npm run lint`, `npm test`, and `npm run build`.

## Cleanup Rule

- Remove entries once they stop being useful for the next few sessions.
- Do not keep historical implementation summaries here.
- If this file starts reading like a backlog or release notes, trim it immediately.
