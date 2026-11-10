using KsefPrinter.ViewModels;
using System.Windows;

namespace KsefPrinter.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        if (DataContext is SettingsViewModel vm) { vm.Saved += (_, _) => Close(); }
    }
}