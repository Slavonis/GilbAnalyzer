using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GilbAnalyzer.Core;

namespace GilbAnalyzer.Gui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        OpenButton.Click += OnOpen;
        AnalyzeButton.Click += OnAnalyze;
        SampleButton.Click += OnSample;
        ClearButton.Click += OnClear;
    }

    // Открытие файла .js через системный диалог.
    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите файл JavaScript",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JavaScript") { Patterns = new[] { "*.js", "*.mjs", "*.cjs" } },
                new FilePickerFileType("Все файлы") { Patterns = new[] { "*.*" } }
            }
        });

        var file = files?.FirstOrDefault();
        if (file == null) return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            SourceBox.Text = await reader.ReadToEndAsync();
            FileLabel.Text = "Файл: " + file.Name;
            Analyze();
        }
        catch (Exception ex)
        {
            FileLabel.Text = "Ошибка чтения файла: " + ex.Message;
        }
    }

    private void OnAnalyze(object? sender, RoutedEventArgs e) => Analyze();

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        SourceBox.Text = string.Empty;
        FileLabel.Text = "Файл не выбран";
        BreakdownGrid.ItemsSource = null;
        CLValue.Text = "0";
        ClValue.Text = "0,000";
        ClFormula.Text = "—";
        CLIValue.Text = "0";
    }

    private void OnSample(object? sender, RoutedEventArgs e)
    {
        SourceBox.Text = SampleCode;
        FileLabel.Text = "Загружен встроенный пример";
        Analyze();
    }

    // Запуск анализа и вывод результатов.
    private void Analyze()
    {
        string code = SourceBox.Text ?? string.Empty;
        GilbResult r = GilbCalculator.Analyze(code);

        BreakdownGrid.ItemsSource = new List<BreakdownEntry>(r.Breakdown);

        CLValue.Text = r.CL.ToString();
        ClValue.Text = r.Cl.ToString("0.000");
        ClFormula.Text = $"CL / N = {r.CL} / {r.N} = {r.Cl:0.000}";
        CLIValue.Text = r.CLI.ToString();
    }

    // Встроенный пример (аналог tests/sample.js): все циклы + ветвления + switch.
    private const string SampleCode =
@"function analyzeNumbers(numbers) {
    var stats = { positive: 0, negative: 0, zero: 0 };

    for (var i = 0; i < numbers.length; i++) {
        var value = numbers[i];
        if (value > 0) {
            stats.positive = stats.positive + 1;
        } else if (value < 0) {
            stats.negative = stats.negative + 1;
        } else {
            stats.zero = stats.zero + 1;
        }
    }

    var sum = 0;
    for (var num of numbers) {
        sum += num;
    }

    var report = """";
    for (var key in stats) {
        report = report + key + ""="" + stats[key] + ""; "";
    }

    var count = numbers.length;
    var doubled = [];
    while (count > 0) {
        count = count - 1;
        doubled.push(numbers[count] * 2);
    }

    var attempts = 0;
    do {
        attempts = attempts + 1;
    } while (attempts < 3);

    return { stats: stats, sum: sum, report: report, doubled: doubled };
}

function classify(avg) {
    var grade;
    for (var i = 0; i < numbers.length; i++) {
        switch (avg) {
            case avg >= 90:
                grade = ""A"";
                break;
            case avg >= 75:
                grade = ""B"";
                break;
            case avg >= 60:
                grade = ""C"";
                break;
            default:
                grade = ""F"";
        }
    }
    return grade;
}

var data = [5, -3, 0, 8, -1, 4];
var result = analyzeNumbers(data);
var average = result.sum / data.length;
var label = (average >= 0) ? ""неотрицательное"" : ""отрицательное"";
console.log(result.report);
console.log(""Среднее: "" + average + "" ("" + label + ""), оценка: "" + classify(average));
";
}
