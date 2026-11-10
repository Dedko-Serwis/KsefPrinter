using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Windows.Input;
using KsefPrinter.Infrastructure;
using KsefPrinter.Models;
using KsefPrinter.Services;

namespace KsefPrinter.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    public event EventHandler? Saved;

    private string _selectedPrinter = "";
    private ItemNameDisplayMode _itemNameDisplay = ItemNameDisplayMode.Wrap;
    private PreferredPriceMode _preferredPrice = PreferredPriceMode.Net;

    private int _itemNameMaxLength = 34;
    private bool _showPrintPreview = true;
    public int ItemNameMaxLength
    {
        get => _itemNameMaxLength;
        set => SetProperty(ref _itemNameMaxLength, value);
    }
    public bool ShowPrintPreview
    {
        get => _showPrintPreview;
        set => SetProperty(ref _showPrintPreview, value);
    }

    public ObservableCollection<string> Printers { get; } = [];

    public string[] ItemNameDisplayModes { get; } = ["Zawijaj", "Skrót"];
    public string[] PreferredPriceModes { get; } = ["Netto", "Brutto"];

    public string SelectedItemNameDisplay
    {
        get => ItemNameDisplay == ItemNameDisplayMode.Wrap ? "Zawijaj" : "Skrót";
        set => ItemNameDisplay = value == "Skrót" ? ItemNameDisplayMode.Truncate : ItemNameDisplayMode.Wrap;
    }

    public string SelectedPreferredPrice
    {
        get => PreferredPrice == PreferredPriceMode.Gross ? "Brutto" : "Netto";
        set => PreferredPrice = value == "Brutto" ? PreferredPriceMode.Gross : PreferredPriceMode.Net;
    }

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set => SetProperty(ref _selectedPrinter, value);
    }

    public ItemNameDisplayMode ItemNameDisplay
    {
        get => _itemNameDisplay;
        set
        {
            if (SetProperty(ref _itemNameDisplay, value))
                OnPropertyChanged(nameof(SelectedItemNameDisplay));
        }
    }

    public PreferredPriceMode PreferredPrice
    {
        get => _preferredPrice;
        set
        {
            if (SetProperty(ref _preferredPrice, value))
                OnPropertyChanged(nameof(SelectedPreferredPrice));
        }
    }

    public string[] UpdateCheckOptions { get; } = ["Nie sprawdzaj", "Co 30 dni", "Co 7 dni"];
    private string _selectedUpdateCheck = "Co 7 dni";
    public string SelectedUpdateCheck { get => _selectedUpdateCheck; set => SetProperty(ref _selectedUpdateCheck, value); }
    public ICommand SaveCommand { get; }

    public SettingsViewModel()
    {
        _settingsService = new SettingsService();

        foreach (string printer in PrinterSettings.InstalledPrinters)
            Printers.Add(printer);

        var settings = _settingsService.Load();

        SelectedPrinter = settings.PrinterName;
        ItemNameDisplay = settings.ItemNameDisplay;
        PreferredPrice = settings.PreferredPrice;
        ItemNameMaxLength = settings.ItemNameMaxLength;
        ShowPrintPreview = settings.ShowPrintPreview;
        SelectedUpdateCheck = settings.UpdateCheckDays switch { 0 => "Nie sprawdzaj", 30 => "Co 30 dni", _ => "Co 7 dni" };

        SaveCommand = new RelayCommand(_ => Save());
    }

    private void Save()
    {
        if (ItemNameMaxLength < 5 || ItemNameMaxLength > 500)
        {
            System.Windows.MessageBox.Show("Liczba znaków musi być w zakresie 5–500.", "Ustawienia");
            return;
        }
        var settings = _settingsService.Load();
        settings.PrinterName = SelectedPrinter;
        settings.ItemNameDisplay = ItemNameDisplay;
        settings.PreferredPrice = PreferredPrice;
        settings.ItemNameMaxLength = ItemNameMaxLength;
        settings.ShowPrintPreview = ShowPrintPreview;
        settings.UpdateCheckDays = SelectedUpdateCheck switch { "Nie sprawdzaj" => 0, "Co 30 dni" => 30, _ => 7 };
        _settingsService.Save(settings);


        Saved?.Invoke(this, EventArgs.Empty);
    }
}
