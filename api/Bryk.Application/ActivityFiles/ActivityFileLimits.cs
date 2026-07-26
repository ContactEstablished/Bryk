namespace Bryk.Application.ActivityFiles;

public static class ActivityFileLimits
{
    /// <summary>Largest accepted activity file (ADR-0010 §2). Enforced by the upload validator → 400.</summary>
    public const int MaxBytes = 25 * 1024 * 1024;

    /// <summary>
    /// The framework-level ceiling on the upload action, deliberately above <see cref="MaxBytes"/> so a
    /// slightly-oversized file is rejected by our validator with a clean 400 instead of being killed by
    /// the request pipeline (whose exceptions the global middleware maps to 500).
    /// </summary>
    public const long HardCapBytes = 32L * 1024 * 1024;
}
