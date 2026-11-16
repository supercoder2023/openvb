using System.Globalization;

namespace OpenVBStudio.Core.Language;

public sealed class BasicInterpreter(BasicCompiler? compiler = null)
{
    private readonly BasicCompiler _compiler = compiler ?? new BasicCompiler();
    private const int MaximumLoopIterations = 100_000;

    public void Execute(
        string source,
        string? procedureName = null,
        Action<string>? showMessage = null,
        Action<string>? showForm = null,
        Func<string, string, object?>? getControlProperty = null)
    {
        var program = _compiler.Compile(source);
        Action<string> formDisplay = showForm ?? (name =>
            throw new InvalidOperationException($"Cannot show form '{name}' because no form host is available."));
        Func<string, string, object?> controlPropertyReader = getControlProperty ?? ((control, property) =>
            throw new InvalidOperationException($"Cannot read {control}.{property} because no control host is available."));
        var context = new ExecutionContext(program, showMessage ?? (_ => { }), formDisplay, controlPropertyReader);
        if (procedureName is null)
        {
            context.ExecuteStatements(program.Statements);
            return;
        }
        context.Invoke(procedureName, []);
    }

    private sealed class ExecutionContext(
        BasicProgram program,
        Action<string> showMessage,
        Action<string> showForm,
        Func<string, string, object?> getControlProperty)
    {
        public object? Invoke(string name, IReadOnlyList<object?> arguments)
        {
            if (!program.Procedures.TryGetValue(name, out var procedure))
                throw new InvalidOperationException($"Procedure '{name}' was not found.");
            if (arguments.Count != procedure.Parameters.Count)
                throw new InvalidOperationException($"Procedure '{name}' expects {procedure.Parameters.Count} argument(s).");

            var scope = new Scope();
            for (var index = 0; index < arguments.Count; index++)
                scope.Set(procedure.Parameters[index], arguments[index]);
            if (procedure.IsFunction)
                scope.Declare(procedure.Name, "Variant");
            ExecuteStatements(procedure.Body, scope);
            return procedure.IsFunction ? scope.Get(procedure.Name) : null;
        }

        public void ExecuteStatements(IReadOnlyList<BasicStatement> statements) => ExecuteStatements(statements, new Scope());

        private void ExecuteStatements(IReadOnlyList<BasicStatement> statements, Scope scope)
        {
            foreach (var statement in statements)
            {
                switch (statement)
                {
                    case DimStatement dim:
                        scope.Declare(dim.Name, dim.TypeName);
                        break;
                    case AssignStatement assignment:
                        scope.Set(assignment.Name, Evaluate(assignment.Value, scope));
                        break;
                    case MessageStatement message:
                        showMessage(Convert.ToString(Evaluate(message.Value, scope), CultureInfo.CurrentCulture) ?? "");
                        break;
                    case ShowFormStatement form:
                        showForm(form.FormName);
                        break;
                    case CallStatement call when call.Call is FunctionCallExpression function:
                        EvaluateFunction(function, scope);
                        break;
                    case IfStatement conditional:
                        ExecuteStatements(IsTruthy(Evaluate(conditional.Condition, scope)) ? conditional.ThenBody : conditional.ElseBody, scope);
                        break;
                    case ForStatement loop:
                        ExecuteFor(loop, scope);
                        break;
                    case DoStatement loop:
                        ExecuteDo(loop, scope);
                        break;
                }
            }
        }

        private void ExecuteFor(ForStatement loop, Scope scope)
        {
            var current = ToNumber(Evaluate(loop.Start, scope));
            var end = ToNumber(Evaluate(loop.End, scope));
            var step = loop.Step is null ? 1m : ToNumber(Evaluate(loop.Step, scope));
            if (step == 0)
                throw new InvalidOperationException("For loop Step cannot be zero.");

            for (var count = 0; step > 0 ? current <= end : current >= end; current += step)
            {
                if (count++ >= MaximumLoopIterations)
                    throw new InvalidOperationException("For loop exceeded the execution limit.");
                scope.Set(loop.Variable, current);
                ExecuteStatements(loop.Body, scope);
            }
        }

        private void ExecuteDo(DoStatement loop, Scope scope)
        {
            for (var count = 0; ; count++)
            {
                if (count >= MaximumLoopIterations)
                    throw new InvalidOperationException("Do loop exceeded the execution limit.");
                if (loop.PreCondition is not null && !IsTruthy(Evaluate(loop.PreCondition, scope)))
                    break;
                ExecuteStatements(loop.Body, scope);
                if (loop.PostCondition is not null && !IsTruthy(Evaluate(loop.PostCondition, scope)))
                    break;
            }
        }

        private object? Evaluate(BasicExpression expression, Scope scope) => expression switch
        {
            LiteralExpression literal => literal.Value,
            VariableExpression variable => scope.Get(variable.Name),
            MemberExpression member => getControlProperty(member.ObjectName, member.MemberName),
            UnaryExpression unary => EvaluateUnary(unary, scope),
            BinaryExpression binary => EvaluateBinary(binary, scope),
            FunctionCallExpression function => EvaluateFunction(function, scope),
            _ => throw new InvalidOperationException("Unknown expression.")
        };

        private object? EvaluateFunction(FunctionCallExpression function, Scope scope)
        {
            var arguments = function.Arguments.Select(argument => Evaluate(argument, scope)).ToArray();
            return Invoke(function.Name, arguments);
        }

        private object? EvaluateUnary(UnaryExpression expression, Scope scope)
        {
            var value = Evaluate(expression.Operand, scope);
            return expression.Operator.Equals("Not", StringComparison.OrdinalIgnoreCase)
                ? !IsTruthy(value)
                : -ToNumber(value);
        }

        private object? EvaluateBinary(BinaryExpression expression, Scope scope)
        {
            var left = Evaluate(expression.Left, scope);
            var right = Evaluate(expression.Right, scope);
            return expression.Operator.ToUpperInvariant() switch
            {
                "&" => ToText(left) + ToText(right),
                "+" when left is string || right is string => ToText(left) + ToText(right),
                "+" => ToNumber(left) + ToNumber(right),
                "-" => ToNumber(left) - ToNumber(right),
                "*" => ToNumber(left) * ToNumber(right),
                "/" => ToNumber(left) / ToNumber(right),
                "\\" => decimal.Truncate(ToNumber(left) / ToNumber(right)),
                "MOD" => ToNumber(left) % ToNumber(right),
                "=" => Compare(left, right) == 0,
                "<>" => Compare(left, right) != 0,
                "<" => Compare(left, right) < 0,
                ">" => Compare(left, right) > 0,
                "<=" => Compare(left, right) <= 0,
                ">=" => Compare(left, right) >= 0,
                "AND" => IsTruthy(left) && IsTruthy(right),
                "OR" => IsTruthy(left) || IsTruthy(right),
                _ => throw new InvalidOperationException($"Unsupported operator '{expression.Operator}'.")
            };
        }

        private static int Compare(object? left, object? right)
        {
            if (left is string || right is string)
                return string.Compare(ToText(left), ToText(right), StringComparison.OrdinalIgnoreCase);
            return ToNumber(left).CompareTo(ToNumber(right));
        }

        private static decimal ToNumber(object? value) => value switch
        {
            null => 0,
            bool boolean => boolean ? -1 : 0,
            decimal number => number,
            _ => decimal.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0", CultureInfo.InvariantCulture)
        };

        private static bool IsTruthy(object? value) => value switch
        {
            null => false,
            bool boolean => boolean,
            string text => !string.IsNullOrEmpty(text),
            _ => ToNumber(value) != 0
        };

        private static string ToText(object? value) => Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
    }

    private sealed class Scope
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _types = new(StringComparer.OrdinalIgnoreCase);

        public void Declare(string name, string type)
        {
            _types[name] = type;
            _values[name] = type.ToUpperInvariant() switch
            {
                "STRING" => "",
                "BOOLEAN" => false,
                "INTEGER" or "LONG" => 0m,
                _ => null
            };
        }

        public object? Get(string name) =>
            _values.TryGetValue(name, out var value) ? value : throw new InvalidOperationException($"Variable '{name}' has not been declared.");

        public void Set(string name, object? value)
        {
            if (!_types.TryGetValue(name, out var type))
                _types[name] = type = "Variant";
            _values[name] = type.ToUpperInvariant() switch
            {
                "STRING" => Convert.ToString(value, CultureInfo.CurrentCulture) ?? "",
                "BOOLEAN" => value is bool boolean ? boolean : Convert.ToDecimal(value ?? 0, CultureInfo.InvariantCulture) != 0,
                "INTEGER" => decimal.ToInt32(decimal.Truncate(Convert.ToDecimal(value ?? 0, CultureInfo.InvariantCulture))),
                "LONG" => decimal.ToInt64(decimal.Truncate(Convert.ToDecimal(value ?? 0, CultureInfo.InvariantCulture))),
                _ => value
            };
        }
    }
}
