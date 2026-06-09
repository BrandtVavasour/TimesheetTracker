# Timesheet Tracker — Full App Completion (living checklist)

Tracks the autonomous /loop build to completion. Update checkboxes each iteration.

## A. Database: real persistence, seed/test data, security ✅
- [x] `SeedData` (TestData) file in DataModel — shared sample user/jobs/entries for dev + tests
- [x] `ICurrentUser` accessor (dev = seeded user; later = signed-in Identity user)
- [x] DbContext per-user query filters (`CurrentUserId`) on Job/TimeEntry/JobCustomField/ProjectCode (no IDOR)
- [x] EF-backed data service `ITimesheetData` (async, user-scoped) replacing the in-memory store
- [x] Program wiring: EF InMemory + seed in Development; Npgsql in Production
- [x] Security: Identity password/lockout options, antiforgery, HSTS/HTTPS redirect, secrets via env (never committed)
- [x] Re-accept EF model snapshot after query filters

## B. Screens (all interactive, on `ITimesheetData`)
- [x] Weekly timesheet (table view) — migrated to `ITimesheetData` (async, user-scoped)
- [x] Calendar — month grid, holiday dots, hour bars, click week → weekly (opens via ?week=)
- [x] Jobs editor — settings, custom fields (show-on-timesheet), project codes, archive, state override, decimal places, work days
- [x] Export — pick job + scope (week/month/FY), preview, download `.xlsx` via `IExportService`
- [x] Profile — display name, default state, sign-in methods
- [x] Responsive/mobile: weekly collapses to stacked cards; export/jobs grids stack; icon-only nav

## C. Tests
- [ ] bUnit + Verify.Bunit project (`Web.Tests`) — snapshot weekly grid + key components
- [ ] Data-layer tests for user-scoping / query filters (Testcontainers, Docker-gated)
- [ ] Keep DataModel.Tests green (14 logic/export/model tests)

## D. Screenshots (committed)
- [ ] Capture all 5 screens at desktop (~1440w) and mobile (~390w)
- [ ] Commit PNGs under `docs/screenshots/`

## E. CI / GitHub
- [ ] `.github/workflows/ci.yml` — build + `dotnet test` on push/PR
- [ ] README with build/test/run instructions
- [ ] Verify everything builds + tests pass; commit

## Notes
- Dev/screenshot runs use EF InMemory (no Docker needed). Real Postgres integration is Docker-gated.
- Design source extracted at /tmp/tsdesign (chat + JSX prototype). User chose Table view; employee # click-to-copy.
