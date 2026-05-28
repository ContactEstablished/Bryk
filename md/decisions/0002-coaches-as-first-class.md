# ADR-0002 — Coaches as a first-class user type: v2

**Date:** 2026-05-26
**Status:** Accepted

## Context

`md/product/feature-parity-trainingpeaks.md` tags ~12 features as `candidate` pending a decision on whether coaches are a first-class user type in Bryk. The TrainingPeaks reference product treats coaches as a major surface — coach dashboards, athlete rosters, workout/plan libraries, race-report-per-athlete, HRV syncing to coach view, coach-athlete messaging, notification digests, coach account tiers (revenue).

Bryk's v1 onboarding is athlete-only. Identity is a dev-stub `ICurrentUserService` returning a single Athlete GUID. Real authentication is deferred to Phase 12.

This decision unblocks scoping for any coach-facing work and shapes the auth model that lands in Phase 12.

## Decision

**Coaches are v2.** v1 ships as an athlete-only product. Coach-facing features ship in a post-v1 phase (tentatively Phase 16+).

**One human = one `Athlete`.** There is no separate `User` entity at the domain level. Every authenticated user has exactly one `Athlete` row, and there is no Bryk user who is not also an athlete. This holds for v1 and for v2 — a coach in v2 is an `Athlete` who has been granted a coach role/relationship, not a separate non-athlete identity type.

**The auth-table layout is a Phase 12 implementation decision.** Two reasonable patterns satisfy the conceptual constraint above:

- ASP.NET Core Identity's `ApplicationUser : IdentityUser<Guid>` lives in its own table, linked 1:1 to `Athlete`. Standard ASP.NET pattern; Identity owns password hash / external login plumbing; `Athlete` owns domain fields.
- `Athlete` inherits from `IdentityUser<Guid>` directly. Single table; tighter coupling; less ceremony.

Both are compatible with this ADR. The Phase 12 auth ADR picks one based on Sr. Dev approval and the ASP.NET Identity vs hand-rolled evaluation noted in CLAUDE.md.

## Consequences

**Closed by this decision:**

- ROADMAP Phase 6 Task 6-6 (Coaches decision) — resolved.
- `CLAUDE.md` pending decision "Coaches as a first-class user type" — closed.

**Created by this decision:**

- `md/product/feature-parity-trainingpeaks.md` tag updates needed: all `candidate` entries in Section 4 (Coach: Athlete Management) and Section 5 (Coach: Business & Communication) flip to `v2`. Coach account tiers in Section 7 flip to `v2`. Items already tagged `deferred` (Training Plan Store, Coach Match, public coach profile directory) stay `deferred` — those are revenue/marketplace features dependent on critical mass even after coaches ship.
- v1 design across all phases proceeds assuming athlete-only UX. No coach mode, no coach sidebar, no role-based routing.
- Phase 12 auth ADR must reaffirm the 1:1 user-athlete relationship and pick the table layout. It is a free design choice as long as the conceptual invariant holds.

**Open follow-ups deliberately deferred:**

- v2 coach data model — likely a `CoachingRelationship` table linking one coaching `Athlete` to many coached `Athlete`s, plus a role/flag on `Athlete`. Designed in detail when v2 work opens.
- Coach UI surfaces — dashboard, roster, athlete view, plan library. Defer to v2 phase entry.
- Edge cases not in scope:
  - Parent setting up an account for a child athlete (would require a non-athlete user). Cross that bridge if it becomes a real signal.
  - Strength-only coaches who don't endurance-train themselves. Same — handle if it becomes a real signal.

## Alternatives considered

**v1 — coaches as first-class from launch.** Rejected: significant scope expansion across every phase, auth needs roles day one, v1 cutover slips by months. Bryk's target audience is primarily self-coached athletes; coached athletes are a real but secondary market that can be served post-launch without losing positioning.

**Out of scope — athlete-only permanently.** Rejected: would cut off the coached-athlete market in endurance sports (a meaningful segment), and permanently differentiate Bryk from TrainingPeaks in a way that wasn't a deliberate positioning choice. v2 leaves the option open without committing scope.

**Separate `User` entity owning identity, with `Athlete` as a 1:1 profile attached.** Rejected for this ADR: the user explicitly confirmed every Bryk user is also an athlete, and vice versa, so the `User`/`Athlete` split adds an entity without a conceptual difference. (The auth-table layout in Phase 12 may still split them at the persistence level for ASP.NET Identity reasons — that's a Phase 12 implementation detail, not a domain modeling decision.)
