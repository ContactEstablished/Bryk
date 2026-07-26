using Bryk.Domain.Entities;

namespace Bryk.Application.ActivityFiles;

/// <summary>
/// One activity-file format's parser (ADR-0010 §1). The service (Task 19-4) selects an implementation by
/// matching <see cref="Format"/>; the TCX and GPX implementations are <see cref="System.Xml.Linq"/>-only
/// (no package), and the FIT implementation keeps its SDK dependency inside <c>Bryk.Infrastructure</c>,
/// behind this same interface.
/// </summary>
public interface IActivityFileParser
{
    /// <summary>The file format this parser handles.</summary>
    ActivityFileFormat Format { get; }

    /// <summary>
    /// Parses one activity file into its session aggregates plus an in-memory sample series
    /// (<see cref="ParsedActivity"/> — samples are never persisted, ADR-0010 §6). Throws
    /// <see cref="Exceptions.ValidationException"/> with a single <c>"File: ..."</c> message when the
    /// content is malformed or carries no track data; the caller does not catch it (the global middleware
    /// maps it to 400) and must not have staged anything before calling. Does not dispose
    /// <paramref name="content"/> — that is the caller's stream.
    /// </summary>
    Task<ParsedActivity> ParseAsync(Stream content, CancellationToken ct = default);
}
