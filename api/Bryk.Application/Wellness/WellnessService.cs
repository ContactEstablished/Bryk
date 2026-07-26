using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;

namespace Bryk.Application.Wellness;

public class WellnessService(
    ICurrentUserService currentUser,
    IValidator<WellnessEntryRequest> validator,
    IValidator<WellnessRangeRequest> rangeValidator,
    IDailyWellnessRepository wellnessRepo,
    IUnitOfWork unitOfWork) : IWellnessService
{
    public async Task<WellnessEntryResponse> UpsertAsync(DateOnly date, WellnessEntryRequest request, CancellationToken ct = default)
    {
        // The {date} route segment wins over anything in the body, unconditionally — the URL is the
        // identity of the resource being replaced.
        request.Date = date;

        // Validate FIRST, before any repository call, so an invalid request never touches the database.
        await validator.ValidateOrThrowAsync(request, ct);

        var athleteId = currentUser.GetCurrentAthleteId();

        // THIS READ-THEN-WRITE IS THE IDEMPOTENCY GUARANTEE. The {AthleteId, Date} unique index backs it
        // in SQL Server, but the EF InMemory provider the integration suite runs on enforces no unique
        // index (BrykWebApplicationFactory.cs:11-23), so the service must never rely on the database
        // rejecting a duplicate — it must look first (ADR-0011 §2).
        var existing = await wellnessRepo.GetByAthleteAndDateTrackedAsync(athleteId, date, ct);

        DailyWellness entity;
        if (existing is null)
        {
            entity = new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = athleteId,
                Date = date,
                SleepHours = request.SleepHours,
                SleepQuality = request.SleepQuality,
                RestingHr = request.RestingHr,
                WeightKg = request.WeightKg,
                Soreness = request.Soreness,
                HrvMs = request.HrvMs,
                Notes = request.Notes
            };
            await wellnessRepo.AddAsync(entity, ct);
        }
        else
        {
            // All seven fields, including the nulls: PUT replaces the whole day (ADR-0011 §2).
            existing.SleepHours = request.SleepHours;
            existing.SleepQuality = request.SleepQuality;
            existing.RestingHr = request.RestingHr;
            existing.WeightKg = request.WeightKg;
            existing.Soreness = request.Soreness;
            existing.HrvMs = request.HrvMs;
            existing.Notes = request.Notes;

            wellnessRepo.Update(existing);
            entity = existing;
        }

        // One commit, covering both branches. CreatedAt/UpdatedAt are the interceptor's — never set here.
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<IReadOnlyList<WellnessEntryResponse>> GetRangeAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        await rangeValidator.ValidateOrThrowAsync(new WellnessRangeRequest { From = from, To = to }, ct);

        var athleteId = currentUser.GetCurrentAthleteId();
        var entries = await wellnessRepo.GetByAthleteInRangeAsync(athleteId, from!.Value, to!.Value, ct);

        // Sparse and already ascending — the repository orders by Date.
        return entries.Select(Map).ToList();
    }

    public async Task<WellnessSummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Exactly the 14 days the calculator needs: the current 7-day window plus the prior 7.
        var entries = await wellnessRepo.GetByAthleteInRangeAsync(athleteId, today.AddDays(-13), today, ct);

        return WellnessSummaryCalculator.Compute(entries, today);
    }

    private static WellnessEntryResponse Map(DailyWellness w) => new()
    {
        Id = w.Id,
        Date = w.Date,
        SleepHours = w.SleepHours,
        SleepQuality = w.SleepQuality,
        RestingHr = w.RestingHr,
        WeightKg = w.WeightKg,
        Soreness = w.Soreness,
        HrvMs = w.HrvMs,
        Notes = w.Notes
    };
}
