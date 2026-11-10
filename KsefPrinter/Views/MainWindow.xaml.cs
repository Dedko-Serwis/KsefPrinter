using System.Reflection;
using System.Windows;

namespace KsefPrinter.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "nieznana";
        Title = $"KsefPrinter v{version} — DEDKO-Serwis";
    }
}
