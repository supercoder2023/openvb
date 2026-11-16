using System.Globalization;

namespace OpenVBStudio.Core.Language;

internal sealed class BasicParser(IReadOnlyList<BasicToken> tokens)
{
    private int _position;

    public BasicProgram Parse()
    {
        var statements = new List<BasicStatement>();
        var procedures = new Dictionary<string, ProcedureDeclaration>(StringComparer.OrdinalIgnoreCase);
        SkipNewLines();
        while (Current.Kind != BasicTokenKind.End)
        {
            if (IsKeyword("Private") || IsKeyword("Public"))
                Advance();

            if (IsKeyword("Sub") || IsKeyword("Function"))
            {
                var procedure = ParseProcedure();
                if (!procedures.TryAdd(procedure.Name, procedure))
                    throw Error($"Duplicate procedure '{procedure.Name}'");
            }
            else
            {
                statements.Add(ParseStatement());
            }
            SkipNewLines();
        }

        return new BasicProgram(statements, procedures);
    }

    private ProcedureDeclaration ParseProcedure()
    {
        var start = Advance();
        var isFunction = start.Text.Equals("Function", StringComparison.OrdinalIgnoreCase);
        var name = Expect(BasicTokenKind.Identifier, "Expected a procedure name");
        var parameters = new List<string>();
        if (Match(BasicTokenKind.LeftParen))
        {
            if (!Match(BasicTokenKind.RightParen))
            {
                do
                {
                    var parameter = Expect(BasicTokenKind.Identifier, "Expected a parameter name");
                    parameters.Add(parameter.Text);
                    if (IsKeyword("As"))
                    {
                        Advance();
                        Expect(BasicTokenKind.Identifier, "Expected a parameter type");
                    }
                } while (Match(BasicTokenKind.Comma));
                Expect(BasicTokenKind.RightParen, "Expected ')'");
            }
        }
        if (isFunction && IsKeyword("As"))
        {
            Advance();
            Expect(BasicTokenKind.Identifier, "Expected a function return type");
        }
        RequireLineEnd();
        var body = ParseBlock(() => IsKeyword("End"));
        ExpectKeyword("End");
        if (isFunction)
            ExpectKeyword("Function");
        else
            ExpectKeyword("Sub");
        RequireLineEnd();
        return new ProcedureDeclaration(name.Text, isFunction, parameters, body, start.Line, start.Column);
    }

    private List<BasicStatement> ParseBlock(Func<bool> stop)
    {
        var statements = new List<BasicStatement>();
        SkipNewLines();
        while (Current.Kind != BasicTokenKind.End && !stop())
        {
            statements.Add(ParseStatement());
            SkipNewLines();
        }
        return statements;
    }

    private BasicStatement ParseStatement()
    {
        if (IsKeyword("Dim"))
        {
            var start = Advance();
            var name = Expect(BasicTokenKind.Identifier, "Expected a variable name");
            var typeName = "Variant";
            if (IsKeyword("As"))
            {
                Advance();
                typeName = Expect(BasicTokenKind.Identifier, "Expected a variable type").Text;
            }
            RequireLineEnd();
            return new DimStatement(name.Text, typeName, start.Line, start.Column);
        }

        if (IsKeyword("MsgBox"))
        {
            var start = Advance();
            var value = ParseExpression();
            RequireLineEnd();
            return new MessageStatement(value, start.Line, start.Column);
        }

        if (IsKeyword("If"))
            return ParseIf();
        if (IsKeyword("For"))
            return ParseFor();
        if (IsKeyword("Do"))
            return ParseDo();

        var token = Expect(BasicTokenKind.Identifier, "Expected a statement");
        if (MatchOperator("="))
        {
            var value = ParseExpression();
            RequireLineEnd();
            return new AssignStatement(token.Text, value, token.Line, token.Column);
        }

        if (Match(BasicTokenKind.Dot))
        {
            var member = Expect(BasicTokenKind.Identifier, "Expected a form method after '.'");
            if (!member.Text.Equals("Show", StringComparison.OrdinalIgnoreCase))
                throw Error($"Unsupported form method '{member.Text}'", member);
            RequireLineEnd();
            return new ShowFormStatement(token.Text, token.Line, token.Column);
        }

        var call = ParsePostIdentifier(token);
        RequireLineEnd();
        if (call is not FunctionCallExpression)
            throw Error("Expected an assignment or procedure call", token);
        return new CallStatement(call, token.Line, token.Column);
    }

    private IfStatement ParseIf()
    {
        var start = Advance();
        var condition = ParseExpression();
        ExpectKeyword("Then");
        RequireLineEnd();
        var thenBody = ParseBlock(() => IsKeyword("Else") || IsKeyword("End"));
        var elseBody = new List<BasicStatement>();
        if (IsKeyword("Else"))
        {
            Advance();
            RequireLineEnd();
            elseBody = ParseBlock(() => IsKeyword("End"));
        }
        ExpectKeyword("End");
        ExpectKeyword("If");
        RequireLineEnd();
        return new IfStatement(condition, thenBody, elseBody, start.Line, start.Column);
    }

    private ForStatement ParseFor()
    {
        var start = Advance();
        var variable = Expect(BasicTokenKind.Identifier, "Expected a loop variable");
        ExpectOperator("=");
        var initial = ParseExpression();
        ExpectKeyword("To");
        var end = ParseExpression();
        BasicExpression? step = null;
        if (IsKeyword("Step"))
        {
            Advance();
            step = ParseExpression();
        }
        RequireLineEnd();
        var body = ParseBlock(() => IsKeyword("Next"));
        ExpectKeyword("Next");
        if (Current.Kind == BasicTokenKind.Identifier)
            Advance();
        RequireLineEnd();
        return new ForStatement(variable.Text, initial, end, step, body, start.Line, start.Column);
    }

    private DoStatement ParseDo()
    {
        var start = Advance();
        BasicExpression? preCondition = null;
        BasicExpression? postCondition = null;
        if (IsKeyword("While"))
        {
            Advance();
            preCondition = ParseExpression();
        }
        RequireLineEnd();
        var body = ParseBlock(() => IsKeyword("Loop"));
        ExpectKeyword("Loop");
        if (IsKeyword("While"))
        {
            Advance();
            postCondition = ParseExpression();
        }
        RequireLineEnd();
        return new DoStatement(preCondition, postCondition, body, start.Line, start.Column);
    }

    private BasicExpression ParseExpression(int minimumPrecedence = 0)
    {
        var left = ParseUnary();
        while (TryGetPrecedence(Current, out var precedence, out var op) && precedence >= minimumPrecedence)
        {
            Advance();
            var right = ParseExpression(precedence + 1);
            left = new BinaryExpression(op, left, right, left.Line, left.Column);
        }
        return left;
    }

    private BasicExpression ParseUnary()
    {
        if (Current.Kind == BasicTokenKind.Operator && Current.Text == "-")
        {
            var op = Advance();
            return new UnaryExpression("-", ParseUnary(), op.Line, op.Column);
        }
        if (IsKeyword("Not"))
        {
            var op = Advance();
            return new UnaryExpression("Not", ParseUnary(), op.Line, op.Column);
        }
        return ParsePrimary();
    }

    private BasicExpression ParsePrimary()
    {
        var token = Advance();
        if (token.Kind == BasicTokenKind.Number)
        {
            if (!decimal.TryParse(token.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                throw Error($"Invalid number '{token.Text}'", token);
            return new LiteralExpression(number, token.Line, token.Column);
        }
        if (token.Kind == BasicTokenKind.String)
            return new LiteralExpression(token.Text, token.Line, token.Column);
        if (token.Kind == BasicTokenKind.LeftParen)
        {
            var expression = ParseExpression();
            Expect(BasicTokenKind.RightParen, "Expected ')'");
            return expression;
        }
        if (token.Kind == BasicTokenKind.Identifier)
        {
            if (token.Text.Equals("True", StringComparison.OrdinalIgnoreCase))
                return new LiteralExpression(true, token.Line, token.Column);
            if (token.Text.Equals("False", StringComparison.OrdinalIgnoreCase))
                return new LiteralExpression(false, token.Line, token.Column);
            BasicExpression expression = ParsePostIdentifier(token);
            while (Match(BasicTokenKind.Dot))
            {
                var member = Expect(BasicTokenKind.Identifier, "Expected a member name after '.'");
                expression = new MemberExpression(token.Text, member.Text, token.Line, token.Column);
            }
            return expression;
        }
        throw Error("Expected an expression", token);
    }

    private BasicExpression ParsePostIdentifier(BasicToken token)
    {
        if (!Match(BasicTokenKind.LeftParen))
            return new VariableExpression(token.Text, token.Line, token.Column);
        var arguments = new List<BasicExpression>();
        if (!Match(BasicTokenKind.RightParen))
        {
            do
                arguments.Add(ParseExpression());
            while (Match(BasicTokenKind.Comma));
            Expect(BasicTokenKind.RightParen, "Expected ')'");
        }
        return new FunctionCallExpression(token.Text, arguments, token.Line, token.Column);
    }

    private static bool TryGetPrecedence(BasicToken token, out int precedence, out string op)
    {
        op = token.Text;
        if (token.Kind == BasicTokenKind.Identifier)
            op = token.Text.ToUpperInvariant();
        precedence = op switch
        {
            "OR" => 1,
            "AND" => 2,
            "=" or "<>" or "<" or ">" or "<=" or ">=" => 3,
            "&" or "+" or "-" => 4,
            "*" or "/" or "\\" or "MOD" => 5,
            _ => -1
        };
        return precedence >= 0 && (token.Kind == BasicTokenKind.Operator || op is "OR" or "AND" or "MOD");
    }

    private bool IsKeyword(string value) => Current.Kind == BasicTokenKind.Identifier && Current.Text.Equals(value, StringComparison.OrdinalIgnoreCase);
    private void ExpectKeyword(string value)
    {
        if (!IsKeyword(value))
            throw Error($"Expected '{value}'");
        Advance();
    }

    private bool MatchOperator(string value)
    {
        if (Current.Kind != BasicTokenKind.Operator || Current.Text != value)
            return false;
        Advance();
        return true;
    }
    private void ExpectOperator(string value)
    {
        if (!MatchOperator(value))
            throw Error($"Expected '{value}'");
    }

    private BasicToken Expect(BasicTokenKind kind, string message)
    {
        if (Current.Kind != kind)
            throw Error(message);
        return Advance();
    }

    private bool Match(BasicTokenKind kind)
    {
        if (Current.Kind != kind)
            return false;
        Advance();
        return true;
    }

    private void RequireLineEnd()
    {
        if (Current.Kind == BasicTokenKind.NewLine)
            SkipNewLines();
        else if (Current.Kind != BasicTokenKind.End)
            throw Error("Expected end of line");
    }

    private void SkipNewLines()
    {
        while (Current.Kind == BasicTokenKind.NewLine)
            Advance();
    }

    private BasicToken Advance()
    {
        var token = Current;
        if (Current.Kind != BasicTokenKind.End)
            _position++;
        return token;
    }

    private BasicToken Current => tokens[Math.Min(_position, tokens.Count - 1)];
    private BasicDiagnosticException Error(string message, BasicToken? token = null)
    {
        var location = token ?? Current;
        return new BasicDiagnosticException(message, location.Line, location.Column);
    }
}
