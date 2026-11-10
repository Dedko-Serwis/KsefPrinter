using System.Collections.Concurrent;

namespace KsefPrinter.Services;

public class PrintQueueService
{
    public ConcurrentQueue<string> Queue { get; } = new();
}