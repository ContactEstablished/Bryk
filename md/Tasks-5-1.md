# Task 5-1 — Server validation error mapping for onboarding forms

## Goal
Map the current API validation error shape from `Bryk.Application.Exceptions.ValidationException` into per-field vee-validate errors on all three onboarding steps. Do **not** change the API error response shape in this task.

Current backend shape from `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs`:

```json
{
  "status": 400,
  "error": "One or more validation errors occurred.",
  "errors": ["..."] ,
  "traceId": "..."
}
```

Current UI state already complete in code:
- `ui/src/services/api.ts` has a shared `ApiError` and all HTTP flows through `apiFetch`.
- `ui/src/services/onboarding.ts` exposes typed methods for the four Phase 4 onboarding endpoints.
- `RequiredStep.vue`, `RecommendedStep.vue`, and `GoalsStep.vue` catch `ApiError`, but only show a global message today.

## Acceptance criteria
- Add a small reusable UI helper for onboarding/API validation errors, likely under `ui/src/services/` or `ui/src/lib/`.
- The helper recognizes `ApiError` 400 responses whose body includes an `errors: string[]` array.
- For known FluentValidation messages, the helper maps errors to the closest vee-validate field path for each step:
  - Required: `name`, `gender`, `dateOfBirth`, `heightCm`, `weightKg`, `yearsTraining`, `typicalWeeklyHours`, `methodology`.
  - Recommended: `restingHr`, `maxHr`, and sport threshold paths such as `sportThresholds[0].thresholdValue`, `sportThresholds[0].lt1`, `sportThresholds[0].lt2` when the message can be confidently mapped.
  - Goals: event and goal field paths when the message includes enough property context, otherwise global error.
- Unknown/unmappable server validation messages are still shown in a global error area; no errors are swallowed.
- Each onboarding step uses `form.setFieldError(...)` for mapped server errors.
- Do not introduce RFC 7807 or change server middleware.
- `npm run build` from `ui/` succeeds after dependencies are installed.

## Files likely to change
- `ui/src/services/api.ts` or new `ui/src/services/apiErrors.ts`
- `ui/src/components/onboarding/RequiredStep.vue`
- `ui/src/components/onboarding/RecommendedStep.vue`
- `ui/src/components/onboarding/GoalsStep.vue`

## What NOT to modify
- Do not change backend middleware or `ValidationException` shape.
- Do not add new packages.
- Do not refactor unrelated component structure or styling.
- Do not change onboarding endpoint URLs or DTO names.

## Test plan
1. Run `npm run build` from `ui/`.
2. Manually inspect that all three step components still submit via the Pinia store and do not call `fetch`/`axios` directly.
3. Simulate representative `ApiError` bodies or use temporary local testing to confirm at least one mapped and one unmapped validation error render correctly.
