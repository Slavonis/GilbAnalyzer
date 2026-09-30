using Avalonia;
using System;

namespace GilbAnalyzer.Gui;

internal static class Program
{
    // Точка входа приложения. Инициализирует Avalonia и запускает окно.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
