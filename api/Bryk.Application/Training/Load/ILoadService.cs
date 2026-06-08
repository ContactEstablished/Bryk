using Bryk.Domain.Entities;

namespace Bryk.Application.Training.Load;

/// <summary>
/// Computes a planned workout's training load (TSS) on read (ADR-0005 §1–3). Loads the current athlete's
/// sport threshold + effective zones and delegates the math to <see cref="LoadCalculator"/>. Pure-math
/// callers that already hold the context (e.g. the weekly batch in Task 11-2) use <see cref="LoadCalculator"/>
/// directly to avoid per-workout I/O.
/// </summary>
public interface ILoadService
{
    /// <summary>
    /// Computes planned load for one workout (with its <see cref="PlannedWorkout.Blocks"/> loaded). Returns
    /// null when there is no structure to compute from.
    /// </summary>
    Task<decimal?> ComputePlannedLoadAsync(PlannedWorkout workout, CancellationToken ct = default);
}
