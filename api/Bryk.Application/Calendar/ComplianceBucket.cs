namespace Bryk.Application.Calendar;

/// <summary>
/// The 5 compliance buckets (ADR-0008 §1). <c>Unplanned</c> is a flag on a completed item, not a bucket.
/// </summary>
public enum ComplianceBucket
{
    Grey = 0,
    Green = 1,
    Yellow = 2,
    Red = 3
}
