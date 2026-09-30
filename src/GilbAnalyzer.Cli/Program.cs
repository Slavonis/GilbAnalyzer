using System;
using System.IO;
using GilbAnalyzer.Core;

// Консольный вариант анализатора метрик Джилба (для проверки логики и отчёта).
// Использование:  dotnet run --project src/GilbAnalyzer.Cli -- <файл.js>

if (args.Length < 1)
{
    Console.WriteLine("Использование: GilbAnalyzer.Cli <путь к .js файлу>");
    return 1;
}

string path = args[0];
if (!File.Exists(path))
{
    Console.WriteLine($"Файл не найден: {path}");
    return 1;
}

var r = GilbCalculator.Analyze(File.ReadAllText(path));

Console.WriteLine($"Файл: {path}");
Console.WriteLine(new string('=', 60));
Console.WriteLine("Детализация управляющих конструкций:");
Console.WriteLine($"  {"Конструкция",-32}{"Кол-во",8}{"Вклад в CL",12}");
Console.WriteLine("  " + new string('-', 52));
foreach (var b in r.Breakdown)
    Console.WriteLine($"  {b.Construct,-32}{b.Count,8}{b.ContributionToCL,12}   {b.Note}");

Console.WriteLine();
Console.WriteLine("РЕЗУЛЬТАТ (метрики Джилба):");
Console.WriteLine($"  Абсолютная сложность      CL  = {r.CL}");
Console.WriteLine($"  Общее число операторов    N   = {r.N}");
Console.WriteLine($"  Относительная сложность    cl  = CL / N = {r.CL} / {r.N} = {r.Cl:F3}");
Console.WriteLine($"  Макс. уровень вложенности  CLI = {r.CLI}");

return 0;
