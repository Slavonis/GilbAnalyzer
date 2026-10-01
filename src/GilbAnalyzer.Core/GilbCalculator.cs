using System;
using System.Collections.Generic;
using System.Linq;

namespace GilbAnalyzer.Core;

/// <summary>
/// Синтаксический анализатор, вычисляющий метрики Джилба (метрики сложности
/// потока управления) для исходного текста на JavaScript.
///
/// ОПРЕДЕЛЕНИЯ:
///
///   CL  — абсолютная сложность программы = количество условных операторов.
///   cl  — относительная сложность = CL / N, где N — общее количество
///         операторов программы.
///   CLI — максимальный уровень вложенности условного оператора.
///
/// Что считается условным оператором (вклад в CL):
///   * каждый оператор ветвления if (в т.ч. else-if) — 1;
///   * каждый оператор цикла (for, for-of, for-in, while, do-while) — 1;
///   * каждый тернарный оператор ?: — 1;
///   * оператор switch дает вклад в CL, равный количеству ветвей case
///     (ветка default не учитывается).
///
/// Максимальный уровень вложенности CLI (считается от 0):
///   * Одиночный условный оператор или цикл на верхнем уровне имеет уровень 0.
///   * Условие, вложенное в цикл, имеет уровень 1 (и т.д.).
///   * Конструкция switch с n ветвями case образует каскад, формируя
///     собственную вложенность (n - 1).
/// </summary>
public static class GilbCalculator
{
    private enum CondKind { If, For, While, Do, Switch, Ternary }

    private sealed class Conditional
    {
        public CondKind Kind;
        public int HeaderPos;      // индекс токена-заголовка
        public int SpanStart;      // левая граница охвата (эксклюзивно)
        public int SpanEnd;        // правая граница охвата (эксклюзивно)
        public int EnclosingWeight;// сколько уровней вложенности добавляет дочерним элементам
        public int SelfNesting;    // собственная максимальная вложенность (например, для switch)
        public int Branches;       // для switch — число ветвей case
    }

    public static GilbResult Analyze(string source)
    {
        var all = new JsTokenizer(source).Tokenize();
        var t = all.Where(x => x.Kind != TokenKind.Eof).ToList();

        var parenMatch = MatchPairs(t, "(", ")");
        var braceMatch = MatchPairs(t, "{", "}");
        int[] curlyDepth = ComputeCurlyDepth(t);

        // Индексы токенов while, являющихся хвостом do-while
        var doTailWhile = new HashSet<int>();

        var conds = new List<Conditional>();
        int ifCount = 0, forCount = 0, whileCount = 0, doCount = 0;
        int switchCount = 0, switchBranches = 0, ternaryCount = 0;

        for (int i = 0; i < t.Count; i++)
        {
            var tok = t[i];

            if (tok.Kind == TokenKind.Keyword)
            {
                switch (tok.Text)
                {
                    case "if":
                    {
                        var c = MakeHeaderCond(t, i, parenMatch, braceMatch, CondKind.If);
                        ExtendSpanOverElse(t, c, parenMatch, braceMatch);
                        conds.Add(c);
                        ifCount++;
                        break;
                    }
                    case "for":
                    {
                        conds.Add(MakeHeaderCond(t, i, parenMatch, braceMatch, CondKind.For));
                        forCount++;
                        break;
                    }
                    case "do":
                    {
                        var c = MakeDoCond(t, i, braceMatch, doTailWhile);
                        conds.Add(c);
                        doCount++;
                        break;
                    }
                    case "while":
                    {
                        if (doTailWhile.Contains(i)) break; 
                        conds.Add(MakeHeaderCond(t, i, parenMatch, braceMatch, CondKind.While));
                        whileCount++;
                        break;
                    }
                    case "switch":
                    {
                        var c = MakeSwitchCond(t, i, parenMatch, braceMatch, curlyDepth);
                        conds.Add(c);
                        switchCount++;
                        switchBranches += c.Branches;
                        break;
                    }
                }
            }
            else if (tok.Kind == TokenKind.Punctuator && tok.Text == "?")
            {
                conds.Add(new Conditional
                {
                    Kind = CondKind.Ternary,
                    HeaderPos = i,
                    SpanStart = i,
                    SpanEnd = i,
                    EnclosingWeight = 1,
                    SelfNesting = 0
                });
                ternaryCount++;
            }
        }

        // ----- Абсолютная сложность CL -----
        // Вклад switch равен строго количеству веток case (default проигнорирован)
        int switchCLContribution = conds.Where(c => c.Kind == CondKind.Switch).Sum(c => c.Branches);
        int loops = forCount + whileCount + doCount;
        int cl = ifCount + loops + ternaryCount + switchCLContribution;

        // ----- Простые операторы и N -----
        int simpleStatements = CountSimpleStatements(t, doCount);
        int n = simpleStatements + cl;

        // ----- Максимальный уровень вложенности CLI -----
        int cli = 0;
        foreach (var c in conds)
        {
            // Считаем количество внешних конструкций, охватывающих текущую
            int enclosing = conds
                .Where(d => d != c && d.SpanStart < c.HeaderPos && c.HeaderPos < d.SpanEnd)
                .Sum(d => d.EnclosingWeight);
            
            // Уровень = кол-во охватывающих конструкций + собственная глубина конструкции
            int level = enclosing + c.SelfNesting;
            
            if (level > cli) cli = level;
        }

        // ----- Результат + детализация -----
        var res = new GilbResult
        {
            CL = cl,
            N = n,
            CLI = cli,
            IfCount = ifCount,
            ForCount = forCount,
            WhileCount = whileCount,
            DoCount = doCount,
            SwitchCount = switchCount,
            SwitchBranches = switchBranches,
            TernaryCount = ternaryCount,
            SimpleStatements = simpleStatements
        };

        res.Breakdown.Add(new BreakdownEntry("if / else-if (ветвление)", ifCount, ifCount));
        res.Breakdown.Add(new BreakdownEntry("for / for-of / for-in", forCount, forCount, "цикл"));
        res.Breakdown.Add(new BreakdownEntry("while", whileCount, whileCount, "цикл"));
        res.Breakdown.Add(new BreakdownEntry("do-while", doCount, doCount, "цикл"));
        res.Breakdown.Add(new BreakdownEntry("switch (множественный выбор)", switchCount,
            switchCLContribution,
            switchCount > 0 ? $"Σ ветвей case = {switchBranches}; вклад = Σ(case)" : ""));
        res.Breakdown.Add(new BreakdownEntry("тернарный ?:", ternaryCount, ternaryCount));
        res.Breakdown.Add(new BreakdownEntry("прочие операторы", simpleStatements, 0,
            "не условные; входят только в N"));

        return res;
    }

    // ---------------- построение условных операторов ----------------

    private static Conditional MakeHeaderCond(
        List<Token> t, int i, Dictionary<int, int> parenMatch,
        Dictionary<int, int> braceMatch, CondKind kind)
    {
        var c = new Conditional { Kind = kind, HeaderPos = i, EnclosingWeight = 1, SelfNesting = 0 };
        int lparen = NextIndex(t, i, "(");
        int bodyStart;
        if (lparen >= 0 && parenMatch.TryGetValue(lparen, out int rparen))
            bodyStart = rparen + 1;
        else
            bodyStart = i + 1;

        SetBodySpan(t, c, bodyStart, braceMatch);
        return c;
    }

    private static Conditional MakeDoCond(
        List<Token> t, int i, Dictionary<int, int> braceMatch, HashSet<int> doTailWhile)
    {
        var c = new Conditional { Kind = CondKind.Do, HeaderPos = i, EnclosingWeight = 1, SelfNesting = 0 };
        SetBodySpan(t, c, i + 1, braceMatch);

        int afterBody = c.SpanEnd; 
        for (int k = afterBody + 1; k < t.Count; k++)
        {
            if (t[k].Kind == TokenKind.Keyword && t[k].Text == "while")
            {
                doTailWhile.Add(k);
                break;
            }
            if (t[k].Kind == TokenKind.Keyword) break;
        }
        return c;
    }

    private static Conditional MakeSwitchCond(
        List<Token> t, int i, Dictionary<int, int> parenMatch,
        Dictionary<int, int> braceMatch, int[] curlyDepth)
    {
        var c = new Conditional { Kind = CondKind.Switch, HeaderPos = i };
        int lparen = NextIndex(t, i, "(");
        int bodyStart = (lparen >= 0 && parenMatch.TryGetValue(lparen, out int rparen))
            ? rparen + 1 : i + 1;

        SetBodySpan(t, c, bodyStart, braceMatch);

        // Подсчёт ветвей (только case, default игнорируем)
        int cases = 0;
        if (c.SpanStart >= 0 && c.SpanEnd > c.SpanStart)
        {
            int innerDepth = curlyDepth[c.SpanStart] + 1; 
            for (int k = c.SpanStart + 1; k < c.SpanEnd; k++)
            {
                if (t[k].Kind == TokenKind.Keyword && t[k].Text == "case" && curlyDepth[k] == innerDepth)
                {
                    cases++;
                }
            }
        }
        
        c.Branches = cases;
        c.EnclosingWeight = 1; // Рассматриваем весь блок switch как 1 уровень для элементов внутри него
        c.SelfNesting = Math.Max(cases - 1, 0); // Собственная вложенность ветвей друг в друга (3 кейса = уровень 2)
        return c;
    }

    private static void SetBodySpan(List<Token> t, Conditional c, int bodyStart, Dictionary<int, int> braceMatch)
    {
        if (bodyStart < t.Count && t[bodyStart].Kind == TokenKind.Punctuator && t[bodyStart].Text == "{"
            && braceMatch.TryGetValue(bodyStart, out int close))
        {
            c.SpanStart = bodyStart;
            c.SpanEnd = close;
        }
        else
        {
            int end = bodyStart;
            while (end < t.Count && !(t[end].Kind == TokenKind.Punctuator && t[end].Text == ";")) end++;
            c.SpanStart = Math.Max(bodyStart - 1, 0);
            c.SpanEnd = Math.Min(end, t.Count - 1);
        }
    }

    private static void ExtendSpanOverElse(
        List<Token> t, Conditional c, Dictionary<int, int> parenMatch, Dictionary<int, int> braceMatch)
    {
        int afterThen = c.SpanEnd + 1;
        if (afterThen < t.Count && t[afterThen].Kind == TokenKind.Keyword && t[afterThen].Text == "else")
        {
            int elseBody = afterThen + 1;
            if (elseBody < t.Count && t[elseBody].Kind == TokenKind.Keyword && t[elseBody].Text == "if")
            {
                return;
            }
            if (elseBody < t.Count && t[elseBody].Kind == TokenKind.Punctuator && t[elseBody].Text == "{"
                && braceMatch.TryGetValue(elseBody, out int elseClose))
            {
                c.SpanEnd = elseClose; 
            }
        }
    }

    // ---------------- подсчёт простых операторов ----------------

    private static int CountSimpleStatements(List<Token> t, int doCount)
    {
        int parenDepth = 0;
        int count = 0;
        for (int i = 0; i < t.Count; i++)
        {
            var tok = t[i];
            if (tok.Kind == TokenKind.Punctuator)
            {
                if (tok.Text == "(") parenDepth++;
                else if (tok.Text == ")") { if (parenDepth > 0) parenDepth--; }
                else if (tok.Text == ";" && parenDepth == 0)
                {
                    var prev = i > 0 ? t[i - 1] : null;
                    bool empty = prev != null && prev.Kind == TokenKind.Punctuator &&
                                 (prev.Text == ";" || prev.Text == "{");
                    if (!empty) count++;
                }
            }
        }
        return Math.Max(count - doCount, 0);
    }

    // ---------------- вспомогательные методы ----------------

    private static int NextIndex(List<Token> t, int from, string punct)
    {
        for (int k = from + 1; k < t.Count; k++)
        {
            if (t[k].Kind == TokenKind.Punctuator && t[k].Text == punct) return k;
            if (t[k].Kind == TokenKind.Punctuator && (t[k].Text == "{" || t[k].Text == ";")) return -1;
        }
        return -1;
    }

    private static Dictionary<int, int> MatchPairs(List<Token> t, string open, string close)
    {
        var map = new Dictionary<int, int>();
        var stack = new Stack<int>();
        for (int i = 0; i < t.Count; i++)
        {
            if (t[i].Kind != TokenKind.Punctuator) continue;
            if (t[i].Text == open) stack.Push(i);
            else if (t[i].Text == close && stack.Count > 0) { int o = stack.Pop(); map[o] = i; }
        }
        return map;
    }

    private static int[] ComputeCurlyDepth(List<Token> t)
    {
        var depth = new int[t.Count];
        int d = 0;
        for (int i = 0; i < t.Count; i++)
        {
            if (t[i].Kind == TokenKind.Punctuator && t[i].Text == "}") d = Math.Max(d - 1, 0);
            depth[i] = d;
            if (t[i].Kind == TokenKind.Punctuator && t[i].Text == "{") d++;
        }
        return depth;
    }
}