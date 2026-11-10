using System.Windows;
using System.Windows.Input;

namespace KsefPrinter.Infrastructure;

public static class FileDropCommandBehavior
{
    public static readonly DependencyProperty DropCommandProperty =
        DependencyProperty.RegisterAttached(
            "DropCommand",
            typeof(ICommand),
            typeof(FileDropCommandBehavior),
            new PropertyMetadata(null, OnDropCommandChanged));

    public static ICommand? GetDropCommand(
        DependencyObject obj)
    {
        return (ICommand?)obj.GetValue(DropCommandProperty);
    }

    public static void SetDropCommand(
        DependencyObject obj,
        ICommand value)
    {
        obj.SetValue(DropCommandProperty, value);
    }

    private static void OnDropCommandChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        element.AllowDrop = true;

        element.Drop -= Element_Drop;
        element.Drop += Element_Drop;
    }

    private static void Element_Drop(
        object sender,
        DragEventArgs e)
    {
        if (sender is not DependencyObject d)
            return;

        ICommand? command =
            GetDropCommand(d);

        if (command == null)
            return;

        if (!e.Data.GetDataPresent(
                DataFormats.FileDrop))
            return;

        var files =
            (string[])e.Data.GetData(
                DataFormats.FileDrop);

        if (command.CanExecute(files))
        {
            command.Execute(files);
        }
    }
}