# TrainingPeaks Feature Parity — Bryk

**Source:** TrainingPeaks (https://www.trainingpeaks.com/) — the reference
product Bryk targets for triathlon athlete and coach workflows.

**Intent:** Close feature parity for triathlon-focused athletes and coaches.
Not every feature will ship in v1; this is the candidate inventory used
to scope phase work and post-v1 expansion.

**How to read the status tags:**
- `planned` — already scoped into the 12-phase v1 build
- `candidate` — strong fit, post-v1 or later phase, worth doing
- `deferred` — acknowledged, lower priority or significant scope
- `out-of-scope` — explicitly will not build in Bryk

**Maintenance:** Update status as phases complete or scope decisions are
made. When a feature ships, change its tag to `shipped` and link the
relevant commit or PR.

---

## Decisions Deferred

These scope questions are unresolved and will materially shape the
parity list once answered:

- **Coaches as a first-class user type.** TrainingPeaks treats coaches
  as a separate user role with their own dashboard, athlete roster,
  workout/plan libraries, and revenue features. Bryk's v1 onboarding
  is athlete-only. Decision needed: are coaches v1 scope, v2 expansion,
  or out of scope entirely? All `Coach Features` below are tagged
  `candidate` pending this decision.
- **Indoor virtual training platform.** TrainingPeaks Virtual is a 3D
  cycling sim. Building one is a separate product effort. Tagged
  `deferred` for now.
- **Marketplace and concierge services.** Training Plan Store and Coach
  Match are revenue/marketplace features dependent on having a critical
  mass of coaches. Tagged `deferred`.

---

## 1. Athlete: Planning & Scheduling

- Calendar view (hub for past/future workouts) — `planned`
- Annual Training Plan (ATP) with A/B/C events and auto-calculated weekly load — `planned`
- Compliance color coding (green/yellow/orange/red/grey) — `candidate`
- Calendar drag-and-drop to reschedule — `candidate`
- Weather integration on calendar (7-day) — `candidate`
- Limited Availability tags ("traveling, no bike", "injured") — `candidate`
- Goals & Metrics cards (sleep, soreness, RHR, weight, hydration, menstruation) — `candidate`

## 2. Athlete: Workout Creation & Execution

- Structured workout builder (intervals by power/HR/pace) — `planned`
- Strength workout builder with video demos — `candidate`
- Indoor virtual training platform — `deferred`
- Device sync — push structured workouts to Garmin/Wahoo/Apple Watch — `candidate`
- Daily workout email — `candidate`
- Weekly fitness snapshot email — `candidate`

## 3. Athlete: Analytics & Data Tracking

- Performance Management Chart (PMC) with CTL/ATL/TSB — `planned`
- TSS / IF / NP calculations per sport — `planned`
- Custom thresholds and zones (FTP, LTHR, pace) with auto-zone calculation — `planned`
- Peak Performances (auto-medal personal bests) — `candidate`
- StackUp (benchmark against global database by age/gender/duration) — `candidate`
- Customizable dashboard charts (Time in Zones, HR vs Power decoupling, RHR trends, etc.) — `candidate`
- Workout file analysis (lap splits, power/HR curves, deep-dive views) — `candidate`

## 4. Coach: Athlete Management

> All items below pending the "Coaches as a first-class user type" decision above.

- Coach home dashboard with compliance feed across roster — `candidate`
- Workout and training plan libraries (reusable, organized in folders) — `candidate`
- Athlete groups and group calendar planning — `candidate`
- Bulk copy/paste training across athletes or weeks — `candidate`
- Custom zone methodologies applied globally to athletes — `candidate`
- Race report chart (historical race performance per athlete) — `candidate`
- Health and recovery metric syncing (HRV, RHR) into coach view — `candidate`

## 5. Coach: Business & Communication

> All items below pending the "Coaches as a first-class user type" decision above.

- Post-workout comments (two-way thread per workout) — `candidate`
- In-app chat — `candidate`
- Notification grouping/digest — `candidate`
- Discounted Premium upsell from coach to athlete — `deferred`
- Training Plan Store (marketplace, revenue share) — `deferred`
- Coach Match concierge service — `deferred`
- Public coach profile directory — `deferred`

## 6. Ecosystem, Integrations, Account Tiers

- Two-way sync with Garmin, Wahoo, Apple Health, Coros, Suunto, Polar — `candidate`
- Sync with Zwift, TrainerRoad, Rouvy — `candidate`
- Sync with recovery platforms (Whoop, Oura) — `candidate`
- MyFitnessPal integration (calories in vs out) — `deferred`
- WKO5 desktop integration — `out-of-scope`
- Athlete account tiers (Free / Premium) — `candidate`
- Coach account tiers (per-athlete or unlimited) — `candidate`

---

## Notes for the Architect

- This list is a parity *wishlist*, not a binding spec. Phase scope is
  decided phase-by-phase.
- When starting a new phase, grep this file for adjacent features and
  consider whether any are cheap to fold in.
- When a `candidate` becomes scoped, move it inline into the active
  phase plan and update its status here when shipped.
- The "Decisions Deferred" block is the highest-leverage section. Drive
  those decisions before they block phase work.

---
