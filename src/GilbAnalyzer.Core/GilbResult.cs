using System.Collections.Generic;

namespace GilbAnalyzer.Core;

/// <summary>Строка детализации: тип управляющей конструкции и её количество/вклад.</summary>
public sealed class BreakdownEntry
{
    public string Construct { get; set; }   // название конструкции
    public int Count { get; set; }          // сколько раз встретилась
    public int ContributionToCL { get; set; } // вклад в абсолютную сложность CL
    public string Note { get; set; }        // пояснение

    public BreakdownEntry(string construct, int count, int contributionToCL, string note = "")
    {
        Construct = construct;
        Count = count;
        ContributionToCL = contributionToCL;
        Note = note;
    }
}

/// <summary>
/// Результат расчёта метрик Джилба (метрики сложности потока управления).
/// </summary>
public sealed class GilbResult
{
    // ---- Итоговые метрики Джилба ----
    public int CL { get; set; }         // абсолютная сложность — число условных операторов
    public int N { get; set; }          // общее число операторов программы (классическое, не по Холстеду)
    public double Cl => N > 0 ? (double)CL / N : 0.0; // относительная сложность cl = CL / N
    public int CLI { get; set; }        // максимальный уровень вложенности условного оператора

    // ---- Детализация для наглядности и проверки ----
    public List<BreakdownEntry> Breakdown { get; } = new();

    // Отдельные счётчики составляющих
    public int IfCount { get; set; }        // операторы if (включая else-if)
    public int ForCount { get; set; }       // циклы for / for-of / for-in
    public int WhileCount { get; set; }     // циклы while (без хвоста do-while)
    public int DoCount { get; set; }        // циклы do-while
    public int SwitchCount { get; set; }    // операторы множественного выбора switch
    public int SwitchBranches { get; set; } // суммарное число ветвей во всех switch (Σ n)
    public int TernaryCount { get; set; }   // тернарные операторы ?:
    public int SimpleStatements { get; set; } // прочие (не управляющие) операторы-инструкции
}
