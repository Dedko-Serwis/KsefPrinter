using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace KsefPrinter.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        string version = assembly.GetName().Version?.ToString(3) ?? "nieznana";
        string build = "nieznany";

        try
        {
            string path = assembly.Location;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                build = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (Exception)
        {
            // Wersja pozostaje widoczna także wtedy, gdy odczyt daty EXE nie jest możliwy.
        }

        Title = $"KsefPrinter v{version} | kompilacja: {build}";
    }
}
