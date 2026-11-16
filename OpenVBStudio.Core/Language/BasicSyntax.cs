namespace OpenVBStudio.Core.Language;

internal abstract record BasicStatement(int Line, int Column);
internal sealed record DimStatement(string Name, string TypeName, int Line, int Column) : BasicStatement(Line, Column);
internal sealed record AssignStatement(string Name, BasicExpression Value, int Line, int Column) : BasicStatement(Line, Column);
internal sealed record MessageStatement(BasicExpression Value, int Line, int Column) : BasicStatement(Line, Column);
internal sealed record CallStatement(BasicExpression Call, int Line, int Column) : BasicStatement(Line, Column);
internal sealed record ShowFormStatement(string FormName, int Line, int Column) : BasicStatement(Line, Column);
internal sealed record IfStatement(BasicExpression Condition, IReadOnlyList<BasicStatement> ThenBody, IReadOnlyList<BasicStatement> ElseBody, int Line, int Column) : BasicStatement(Line, Column);
internal sealed record ForStatement(string Variable, BasicExpression Start, BasicExpression End, BasicExpression? Step, IReadOnlyList<BasicStatement> Body, int Line, int Column) : BasicStatement(Line, Column);
internal sealed record DoStatement(BasicExpression? PreCondition, BasicExpression? PostCondition, IReadOnlyList<BasicStatement> Body, int Line, int Column) : BasicStatement(Line, Column);
internal sealed record ProcedureDeclaration(string Name, bool IsFunction, IReadOnlyList<string> Parameters, IReadOnlyList<BasicStatement> Body, int Line, int Column);

internal abstract record BasicExpression(int Line, int Column);
internal sealed record LiteralExpression(object? Value, int Line, int Column) : BasicExpression(Line, Column);
internal sealed record VariableExpression(string Name, int Line, int Column) : BasicExpression(Line, Column);
internal sealed record MemberExpression(string ObjectName, string MemberName, int Line, int Column) : BasicExpression(Line, Column);
internal sealed record UnaryExpression(string Operator, BasicExpression Operand, int Line, int Column) : BasicExpression(Line, Column);
internal sealed record BinaryExpression(string Operator, BasicExpression Left, BasicExpression Right, int Line, int Column) : BasicExpression(Line, Column);
internal sealed record FunctionCallExpression(string Name, IReadOnlyList<BasicExpression> Arguments, int Line, int Column) : BasicExpression(Line, Column);

public sealed class BasicProgram
{
    internal BasicProgram(IReadOnlyList<BasicStatement> statements, IReadOnlyDictionary<string, ProcedureDeclaration> procedures)
    {
        Statements = statements;
        Procedures = procedures;
    }

    internal IReadOnlyList<BasicStatement> Statements { get; }
    internal IReadOnlyDictionary<string, ProcedureDeclaration> Procedures { get; }
}
