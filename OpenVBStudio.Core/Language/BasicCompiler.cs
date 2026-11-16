namespace OpenVBStudio.Core.Language;

public sealed class BasicCompiler
{
    public BasicProgram Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var program = new BasicParser(new BasicLexer(source).Lex()).Parse();
        BasicSemanticAnalyzer.Validate(program);
        return program;
    }
}

internal static class BasicSemanticAnalyzer
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "String", "Integer", "Long", "Boolean", "Variant"
    };

    public static void Validate(BasicProgram program)
    {
        foreach (var procedure in program.Procedures.Values)
        {
            var names = new HashSet<string>(procedure.Parameters, StringComparer.OrdinalIgnoreCase);
            if (names.Count != procedure.Parameters.Count)
                throw new BasicDiagnosticException($"Procedure '{procedure.Name}' has duplicate parameters", procedure.Line, procedure.Column);
            ValidateBlock(procedure.Body, names);
        }
        ValidateBlock(program.Statements, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static void ValidateBlock(IEnumerable<BasicStatement> statements, HashSet<string> names)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case DimStatement dim:
                    if (!SupportedTypes.Contains(dim.TypeName))
                        throw new BasicDiagnosticException($"Unsupported type '{dim.TypeName}'", dim.Line, dim.Column);
                    if (!names.Add(dim.Name))
                        throw new BasicDiagnosticException($"'{dim.Name}' is already declared", dim.Line, dim.Column);
                    break;
                case IfStatement conditional:
                    ValidateBlock(conditional.ThenBody, names);
                    ValidateBlock(conditional.ElseBody, names);
                    break;
                case ForStatement loop:
                    ValidateBlock(loop.Body, names);
                    break;
                case DoStatement loop:
                    ValidateBlock(loop.Body, names);
                    break;
            }
        }
    }
}
