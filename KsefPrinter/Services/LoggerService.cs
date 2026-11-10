using System.IO;

namespace KsefPrinter.Services;

public class LoggerService
{
    private readonly string _folder;

    public LoggerService()
    {
        _folder = Path.Combine(
            AppContext.BaseDirectory,
            "logs");

        Directory.CreateDirectory(_folder);
    }

    public void Log(string text)
    {
        string file =
            Path.Combine(
                _folder,
                $"{DateTime.Now:yyyy-MM-dd}.log");

        string line =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {text}";

        File.AppendAllLines(file, new[] { line });
    }
}