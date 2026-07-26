namespace Bryk.Application.ActivityFiles;

/// <summary>
/// The transport-neutral upload body. <c>IFormFile</c> lives in <c>Microsoft.AspNetCore.Http</c> and
/// <c>Bryk.Application</c> must not reference it (Clean Architecture dependency direction), so the
/// <c>ActivityFilesController</c> is the only place allowed to touch <c>IFormFile</c>; it copies the
/// stream into <see cref="Content"/> and hands this over.
/// </summary>
public class ActivityFileUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
