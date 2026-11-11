using System.Collections.ObjectModel;
using System.Drawing.Printing;
using KsefPrinter.Infrastructure;
using KsefPrinter.Models;
using KsefPrinter.Services;

namespace KsefPrinter.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService = new();
    private bool _loading = true;
    public event EventHandler? Saved;

    private string _selectedPrinter = "";
    private ItemNameDisplayMode _itemNameDisplay = ItemNameDisplayMode.Wrap;
    private PreferredPriceMode _preferredPrice = PreferredPriceMode.Net;
    private int _itemNameMaxLength = 34;
    private bool _showPrintPreview = true;

    public ObservableCollection<string> Printers { get; } = [];
    public string[] ItemNameDisplayModes { get; } = ["Zawijaj", "Ucinaj"];
    public string[] PreferredPriceModes { get; } = ["Netto", "Brutto"];

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set { if (SetProperty(ref _selectedPrinter, value)) Save(); }
    }
    public bool ShowPrintPreview
    {
        get => _showPrintPreview;
        set { if (SetProperty(ref _showPrintPreview, value)) Save(); }
    }
    public ItemNameDisplayMode ItemNameDisplay
    {
        get => _itemNameDisplay;
        set
        {
            if (SetProperty(ref _itemNameDisplay, value))
            {
                OnPropertyChanged(nameof(SelectedItemNameDisplay));
                OnPropertyChanged(nameof(IsTruncateSelected));
                Save();
            }
        }
    }
    public bool IsTruncateSelected => ItemNameDisplay == ItemNameDisplayMode.Truncate;
    public string SelectedItemNameDisplay
    {
        get => IsTruncateSelected ? "Ucinaj" : "Zawijaj";
        set => ItemNameDisplay = value == "Ucinaj" ? ItemNameDisplayMode.Truncate : ItemNameDisplayMode.Wrap;
    }
    public PreferredPriceMode PreferredPrice
    {
        get => _preferredPrice;
        set
        {
            if (SetProperty(ref _preferredPrice, value))
            {
                OnPropertyChanged(nameof(SelectedPreferredPrice));
                Save();
            }
        }
    }
    public string SelectedPreferredPrice
    {
        get => PreferredPrice == PreferredPriceMode.Gross ? "Brutto" : "Netto";
        set => PreferredPrice = value == "Brutto" ? PreferredPriceMode.Gross : PreferredPriceMode.Net;
    }
    public int ItemNameMaxLength
    {
        get => _itemNameMaxLength;
        set
        {
            if (value is < 5 or > 500) return;
            if (SetProperty(ref _itemNameMaxLength, value)) Save();
        }
    }

    public SettingsViewModel()
    {
        foreach (string printer in PrinterSettings.InstalledPrinters)
            Printers.Add(printer);
        var settings = _settingsService.Load();
        SelectedPrinter = settings.PrinterName;
        ItemNameDisplay = settings.ItemNameDisplay;
        PreferredPrice = settings.PreferredPrice;
        ItemNameMaxLength = settings.ItemNameMaxLength;
        ShowPrintPreview = settings.ShowPrintPreview;
        _loading = false;
    }

    private void Save()
    {
        if (_loading) return;
        try
        {
            var settings = _settingsService.Load();
            settings.PrinterName = SelectedPrinter;
            settings.ItemNameDisplay = ItemNameDisplay;
            settings.PreferredPrice = PreferredPrice;
            settings.ItemNameMaxLength = ItemNameMaxLength;
            settings.ShowPrintPreview = ShowPrintPreview;
            _settingsService.Save(settings);
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Nie udało się zapisać ustawień: " + ex.Message, "KsefPrinter");
        }
    }
}
