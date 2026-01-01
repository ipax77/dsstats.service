namespace dsstats.service.Models;

public sealed record UploadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}
