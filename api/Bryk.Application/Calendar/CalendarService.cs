using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Application.Training.Load;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;

namespace Bryk.Application.Calendar;

public class CalendarService(
    ICurrentUserService currentUser,
    ITrainingPlanRepository planRepo,
    IWorkoutRepository workoutRepo,
    IEventRepository eventRepo,
    IAthleteRepository athleteRepo,
    IZoneService zoneService,
    IValidator<CalendarFeedRequest> validator) : ICalendarService
{
    // 42-day default window: today-41 → today (a month of history + the current week + 5 days forward;
    // ADR-0008 §1). Matches the ≤ 62-day cap.
    private const int DefaultHistoryDays = 41;

    public async Task<CalendarFeedResponse> GetFeedAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var request = new CalendarFeedRequest
        {
            From = from ?? today.AddDays(-DefaultHistoryDays),
            To = to ?? today
        };
        await validator.ValidateOrThrowAsync(request, ct);

        var fromDate = request.From!.Value;
        var toDate = request.To!.Value;

        // ── planned ── structure needed for ComputedLoad (ThisWeekService template).
        var planned = await planRepo.GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, fromDate, toDate, ct);
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        var zones = await zoneService.GetZonesAsync(ct);

        // Per-planned precomputation: EffectiveLoad (PlannedLoad ?? ComputedLoad) + PlannedDurationSeconds.
        var plannedEffective = new Dictionary<Guid, (decimal? Load, int? DurationSeconds)>();
        foreach (var pw in planned)
        {
            var profile = athlete?.SportProfiles.FirstOrDefault(p => p.Sport == pw.Sport);
            var sportZones = zones.Sports.FirstOrDefault(s => s.Sport == pw.Sport);
            var computed = LoadCalculator.ComputePlannedLoad(pw, profile, sportZones);
            var effective = pw.PlannedLoad ?? computed;
            var durationSeconds = pw.PlannedDurationMinutes is { } m ? (int?)(m * 60) : null;
            plannedEffective[pw.Id] = (effective, durationSeconds);
        }

        // ── completed ── index by PlannedWorkoutId for the single-match lookup.
        var completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, fromDate, toDate, ct);
        var completedByPlannedId = new Dictionary<Guid, Workout>();
        foreach (var w in completed)
        {
            if (w.PlannedWorkoutId is { } pwId && !completedByPlannedId.ContainsKey(pwId))
            {
                completedByPlannedId[pwId] = w;
            }
        }

        // ── events ──
        var events = await eventRepo.GetByAthleteInRangeAsync(athleteId, fromDate, toDate, ct);

        // ── classify planned + build their chips ──
        var itemsByDate = new Dictionary<DateOnly, List<CalendarItemDto>>();

        // Cache the linked planned's bucket so its completed chip reuses it (no double-classify).
        var plannedBucketById = new Dictionary<Guid, ComplianceBucket>();
        foreach (var pw in planned)
        {
            var (plannedLoad, plannedDur) = plannedEffective[pw.Id];
            Workout? matched = null;
            decimal? completedLoad = null;
            int? completedDurationSeconds = null;

            if (completedByPlannedId.TryGetValue(pw.Id, out var w))
            {
                matched = w;
                completedLoad = w.LoadOverride ?? w.ComputedLoad ?? 0m;
                completedDurationSeconds = w.ActualDurationSeconds;
            }

            var bucket = ComplianceClassifier.Classify(new ComplianceInput(
                ScheduledDate: pw.ScheduledDate,
                PlannedLoad: plannedLoad,
                PlannedDurationSeconds: plannedDur,
                CompletedLoad: completedLoad,
                CompletedDurationSeconds: completedDurationSeconds,
                HasCompletion: matched is not null,
                Today: today));

            plannedBucketById[pw.Id] = bucket;

            AddItem(itemsByDate, pw.ScheduledDate, new CalendarItemDto
            {
                Id = pw.Id,
                Kind = CalendarItemKind.Planned,
                Sport = pw.Sport,
                Title = pw.Title,
                Load = plannedLoad,
                PlannedLoad = plannedLoad,
                Compliance = bucket,
                IsUnplanned = false,
                PlannedWorkoutId = null,
                WorkoutId = matched?.Id,
                TrainingPlanId = pw.TrainingPlanId,
                Priority = null,
                Notes = null
            });
        }

        // ── completed chips ── unplanned: Green + IsUnplanned; linked: reuse the planned's bucket
        // (when the linked planned is in range); linked-but-out-of-range: IsUnplanned stays false,
        // Compliance stays null (we can't grade without the planned's load — honest "—").
        foreach (var w in completed)
        {
            if (w.PlannedWorkoutId is { } pwId)
            {
                if (plannedBucketById.TryGetValue(pwId, out var bucket))
                {
                    var linkedPlanned = planned.First(p => p.Id == pwId);
                    var (linkedLoad, _) = plannedEffective[pwId];
                    AddItem(itemsByDate, w.CompletedDate, new CalendarItemDto
                    {
                        Id = w.Id,
                        Kind = CalendarItemKind.Completed,
                        Sport = w.Sport,
                        Title = linkedPlanned.Title,
                        Load = w.LoadOverride ?? w.ComputedLoad ?? 0m,
                        PlannedLoad = linkedLoad,
                        Compliance = bucket,
                        IsUnplanned = false,
                        PlannedWorkoutId = pwId,
                        WorkoutId = null,
                        TrainingPlanId = null,
                        Priority = null,
                        Notes = null
                    });
                }
                else
                {
                    // Linked to an out-of-range planned — we know it's linked (not unplanned) but can't
                    // grade without the planned's load. Compliance stays null (honest "—").
                    AddItem(itemsByDate, w.CompletedDate, new CalendarItemDto
                    {
                        Id = w.Id,
                        Kind = CalendarItemKind.Completed,
                        Sport = w.Sport,
                        Title = w.Sport.ToString(),
                        Load = w.LoadOverride ?? w.ComputedLoad ?? 0m,
                        PlannedLoad = null,
                        Compliance = null,
                        IsUnplanned = false,
                        PlannedWorkoutId = pwId,
                        WorkoutId = null,
                        TrainingPlanId = null,
                        Priority = null,
                        Notes = null
                    });
                }
            }
            else
            {
                // Unplanned completion: a done-but-unplanned session is a win (ADR-0008 §1).
                AddItem(itemsByDate, w.CompletedDate, new CalendarItemDto
                {
                    Id = w.Id,
                    Kind = CalendarItemKind.Completed,
                    Sport = w.Sport,
                    Title = w.Sport.ToString(), // no linked planned → sport fallback
                    Load = w.LoadOverride ?? w.ComputedLoad ?? 0m,
                    PlannedLoad = null,
                    Compliance = ComplianceBucket.Green,
                    IsUnplanned = true,
                    PlannedWorkoutId = null,
                    WorkoutId = null,
                    TrainingPlanId = null,
                    Priority = null,
                    Notes = null
                });
            }
        }

        // ── events ── not graded.
        foreach (var ev in events)
        {
            AddItem(itemsByDate, ev.EventDate, new CalendarItemDto
            {
                Id = ev.Id,
                Kind = CalendarItemKind.Event,
                Sport = ev.Sport,
                Title = ev.Name,
                Load = null,
                PlannedLoad = null,
                Compliance = null,
                IsUnplanned = false,
                PlannedWorkoutId = null,
                WorkoutId = null,
                TrainingPlanId = null,
                Priority = ev.Priority,
                Notes = ev.Notes
            });
        }

        // ── assemble one day per date in [from, to] inclusive, even empty days ──
        var dayCount = toDate.DayNumber - fromDate.DayNumber + 1;
        var days = new List<CalendarDayDto>(dayCount);
        for (var i = 0; i < dayCount; i++)
        {
            var date = fromDate.AddDays(i);
            var dayItems = itemsByDate.GetValueOrDefault(date) ?? new List<CalendarItemDto>();
            days.Add(new CalendarDayDto
            {
                Date = date,
                Items = OrderItems(dayItems)
            });
        }

        return new CalendarFeedResponse
        {
            RangeStart = fromDate,
            RangeEnd = toDate,
            Days = days
        };
    }

    // Events first (by Priority A→B→C), then planned (by Title), then unplanned completions (by Title).
    private static IReadOnlyList<CalendarItemDto> OrderItems(List<CalendarItemDto> items)
    {
        return items
            .OrderBy(i => i.Kind == CalendarItemKind.Event ? 0 : 1)             // events first
            .ThenBy(i => i.Kind == CalendarItemKind.Event ? i.Priority ?? 0 : 0)
            .ThenBy(i => i.Kind == CalendarItemKind.Planned ? 0 : 1)            // planned before unplanned
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddItem(Dictionary<DateOnly, List<CalendarItemDto>> byDate, DateOnly date, CalendarItemDto item)
    {
        if (!byDate.TryGetValue(date, out var list))
        {
            list = new List<CalendarItemDto>();
            byDate[date] = list;
        }
        list.Add(item);
    }
}
