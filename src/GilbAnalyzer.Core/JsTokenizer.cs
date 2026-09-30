using System;
using System.Collections.Generic;

namespace GilbAnalyzer.Core;

/// <summary>
/// Лексический анализатор (парсер-сканер) исходного текста на JavaScript.
///
/// Задачи сканера:
///  * пропустить пробелы, переводы строк, одно- и многострочные комментарии;
///  * корректно распознать строковые и шаблонные литералы, а также
///    регулярные выражения, НЕ разбирая их содержимое на токены
///    (по условию: содержимое строк, комментариев и regex не учитывается);
///  * выделить числа, идентификаторы/служебные слова и знаки операций.
///
/// Комментарии и пробелы в поток токенов НЕ попадают, поэтому все выданные
/// токены являются «значимыми».
/// </summary>
public sealed class JsTokenizer
{
    // Служебные (зарезервированные) слова JavaScript.
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "break", "case", "catch", "class", "const", "continue", "debugger",
        "default", "delete", "do", "else", "export", "extends", "finally",
        "for", "function", "if", "import", "in", "instanceof", "new",
        "return", "super", "switch", "this", "throw", "try", "typeof",
        "var", "void", "while", "with", "yield", "let", "static", "of",
        "as", "from", "async", "await", "get", "set",
        "true", "false", "null" // литеральные значения — обрабатываются отдельно
    };

    // Многосимвольные знаки операций (проверяются от длинных к коротким).
    private static readonly string[] MultiPunct =
    {
        ">>>=", "===", "!==", ">>>", "**=", "<<=", ">>=", "&&=", "||=", "??=",
        "...", "=>", "?.", "??",
        "==", "!=", "<=", ">=", "&&", "||", "++", "--",
        "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "**", "<<", ">>"
    };

    private readonly string _src;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    public JsTokenizer(string source)
    {
        _src = source ?? string.Empty;
    }

    private char Cur => _pos < _src.Length ? _src[_pos] : '\0';
    private char Peek(int k = 1) => _pos + k < _src.Length ? _src[_pos + k] : '\0';
    private bool End => _pos >= _src.Length;

    private void Advance(int n = 1)
    {
        for (int i = 0; i < n && !End; i++)
        {
            if (_src[_pos] == '\n') { _line++; _col = 1; }
            else { _col++; }
            _pos++;
        }
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        Token? prev = null;

        while (!End)
        {
            char c = Cur;

            // --- пробелы ---
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\f' || c == '\v')
            {
                Advance();
                continue;
            }

            // --- комментарии ---
            if (c == '/' && Peek() == '/')
            {
                while (!End && Cur != '\n') Advance();
                continue;
            }
            if (c == '/' && Peek() == '*')
            {
                Advance(2);
                while (!End && !(Cur == '*' && Peek() == '/')) Advance();
                if (!End) Advance(2);
                continue;
            }

            int line = _line, col = _col;

            // --- строковые литералы ---
            if (c == '"' || c == '\'')
            {
                string s = ReadString(c);
                prev = new Token(TokenKind.String, s, line, col);
                tokens.Add(prev);
                continue;
            }

            // --- шаблонные литералы ---
            if (c == '`')
            {
                string s = ReadTemplate();
                prev = new Token(TokenKind.Template, s, line, col);
                tokens.Add(prev);
                continue;
            }

            // --- регулярное выражение или деление ---
            if (c == '/' && RegexAllowed(prev))
            {
                string s = ReadRegex();
                prev = new Token(TokenKind.Regex, s, line, col);
                tokens.Add(prev);
                continue;
            }

            // --- числа ---
            if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek())))
            {
                string num = ReadNumber();
                prev = new Token(TokenKind.Number, num, line, col);
                tokens.Add(prev);
                continue;
            }

            // --- идентификаторы / служебные слова ---
            if (IsIdentStart(c))
            {
                string id = ReadIdent();
                var kind = Keywords.Contains(id) ? TokenKind.Keyword : TokenKind.Identifier;
                prev = new Token(kind, id, line, col);
                tokens.Add(prev);
                continue;
            }

            // --- знаки операций / разделители ---
            string? mp = MatchMultiPunct();
            if (mp != null)
            {
                Advance(mp.Length);
                prev = new Token(TokenKind.Punctuator, mp, line, col);
                tokens.Add(prev);
                continue;
            }

            // одиночный символ-пунктуатор
            Advance();
            prev = new Token(TokenKind.Punctuator, c.ToString(), line, col);
            tokens.Add(prev);
        }

        tokens.Add(new Token(TokenKind.Eof, "", _line, _col));
        return tokens;
    }

    private static bool IsIdentStart(char c) =>
        char.IsLetter(c) || c == '_' || c == '$';

    private static bool IsIdentPart(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '$';

    private string ReadIdent()
    {
        int start = _pos;
        while (!End && IsIdentPart(Cur)) Advance();
        return _src.Substring(start, _pos - start);
    }

    private string ReadNumber()
    {
        int start = _pos;
        if (Cur == '0' && (Peek() == 'x' || Peek() == 'X' ||
                           Peek() == 'b' || Peek() == 'B' ||
                           Peek() == 'o' || Peek() == 'O'))
        {
            Advance(2);
            while (!End && (char.IsLetterOrDigit(Cur) || Cur == '_')) Advance();
            return _src.Substring(start, _pos - start);
        }
        while (!End && (char.IsDigit(Cur) || Cur == '_')) Advance();
        if (Cur == '.') { Advance(); while (!End && (char.IsDigit(Cur) || Cur == '_')) Advance(); }
        if (Cur == 'e' || Cur == 'E')
        {
            Advance();
            if (Cur == '+' || Cur == '-') Advance();
            while (!End && char.IsDigit(Cur)) Advance();
        }
        if (Cur == 'n') Advance(); // BigInt
        return _src.Substring(start, _pos - start);
    }

    private string ReadString(char quote)
    {
        int start = _pos;
        Advance(); // открывающая кавычка
        while (!End && Cur != quote)
        {
            if (Cur == '\\') Advance(2);
            else Advance();
        }
        if (!End) Advance(); // закрывающая кавычка
        return _src.Substring(start, _pos - start);
    }

    private string ReadTemplate()
    {
        int start = _pos;
        Advance(); // `
        int depth = 0;
        while (!End)
        {
            if (Cur == '\\') { Advance(2); continue; }
            if (depth == 0 && Cur == '`') { Advance(); break; }
            if (Cur == '$' && Peek() == '{') { depth++; Advance(2); continue; }
            if (depth > 0 && Cur == '}') { depth--; Advance(); continue; }
            Advance();
        }
        return _src.Substring(start, _pos - start);
    }

    private string ReadRegex()
    {
        int start = _pos;
        Advance(); // /
        bool inClass = false;
        while (!End)
        {
            char c = Cur;
            if (c == '\\') { Advance(2); continue; }
            if (c == '[') inClass = true;
            else if (c == ']') inClass = false;
            else if (c == '/' && !inClass) { Advance(); break; }
            else if (c == '\n') break;
            Advance();
        }
        while (!End && char.IsLetter(Cur)) Advance(); // флаги
        return _src.Substring(start, _pos - start);
    }

    /// <summary>
    /// Определяет, может ли символ '/' начинать регулярное выражение, а не
    /// операцию деления. Это зависит от предыдущего значимого токена.
    /// </summary>
    private static bool RegexAllowed(Token? prev)
    {
        if (prev == null) return true;
        switch (prev.Kind)
        {
            case TokenKind.Number:
            case TokenKind.String:
            case TokenKind.Template:
            case TokenKind.Regex:
            case TokenKind.Identifier:
                return false; // после значения — это деление
            case TokenKind.Keyword:
                // после return/typeof/... допустимо регулярное выражение,
                // после this/super/true/false/null — нет
                return prev.Text is not ("this" or "super" or "true" or "false" or "null");
            case TokenKind.Punctuator:
                // после ) ] } обычно значение -> деление; иначе -> regex
                return prev.Text is not (")" or "]" or "}");
            default:
                return true;
        }
    }

    private string? MatchMultiPunct()
    {
        foreach (var p in MultiPunct)
        {
            if (_pos + p.Length <= _src.Length &&
                string.CompareOrdinal(_src, _pos, p, 0, p.Length) == 0)
            {
                return p;
            }
        }
        return null;
    }
}
