using dsstats.service.Models;
using System.Collections.Concurrent;

namespace dsstats.service;

public sealed partial class DsstatsService
{
    // SQLite single-writer enforcement
    private readonly SemaphoreSlim _dbSemaphore = new(1, 1);
    public SemaphoreSlim DbSemaphore => _dbSemaphore;
    private readonly SemaphoreSlim _runGate = new(1, 1);

    // Progress counters
    private DateTime _start;
    private int _decoded;
    private int _imported;
    private int _errors;
    private int _total;
    private UploadStatus _uploadStatus;
    private ConcurrentBag<ReplayError> _replayErrors = [];

    #region Progress Reporting

    private void ResetCounters()
    {
        _start = DateTime.UtcNow;
        _total = 0;
        _decoded = 0;
        _imported = 0;
        _errors = 0;
        _uploadStatus = UploadStatus.None;
        _replayErrors.Clear();
    }

    #endregion
}
