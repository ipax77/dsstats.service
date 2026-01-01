namespace dsstats.service.Models;

public record DecodeStatus(
    int TotalInDb,
    int NewInFolders,
    List<string> ToDoReplayPaths);
