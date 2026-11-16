namespace OpenVBStudio.Core.Language;

internal enum BasicTokenKind
{
    Identifier,
    Number,
    String,
    NewLine,
    Operator,
    LeftParen,
    RightParen,
    Comma,
    Dot,
    End
}

internal readonly record struct BasicToken(BasicTokenKind Kind, string Text, int Line, int Column);

public sealed class BasicDiagnosticException(string message, int line, int column)
    : Exception($"{message} (line {line}, column {column})")
{
    public int Line { get; } = line;
    public int Column { get; } = column;
}
