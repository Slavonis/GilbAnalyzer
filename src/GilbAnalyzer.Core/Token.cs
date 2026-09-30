namespace GilbAnalyzer.Core;

/// <summary>
/// Категория лексемы (токена), которую выделяет лексический анализатор.
/// </summary>
public enum TokenKind
{
    Identifier, // имя переменной / функции / свойства
    Keyword,    // служебное слово JavaScript (if, for, function, ...)
    Number,     // числовой литерал
    String,     // строковый литерал '...' или "..."
    Template,   // шаблонный литерал `...`
    Regex,      // регулярное выражение /.../flags
    Punctuator, // знак операции или разделитель ( ) { } ; , + - * ...
    Eof         // конец входного текста
}

/// <summary>
/// Одна лексема исходного текста.
/// </summary>
public sealed class Token
{
    public TokenKind Kind { get; }
    public string Text { get; }
    public int Line { get; }
    public int Column { get; }

    public Token(TokenKind kind, string text, int line, int column)
    {
        Kind = kind;
        Text = text;
        Line = line;
        Column = column;
    }

    public override string ToString() => $"{Kind}:'{Text}' ({Line}:{Column})";
}
