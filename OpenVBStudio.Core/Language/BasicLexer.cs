namespace OpenVBStudio.Core.Language;

internal sealed class BasicLexer(string source)
{
    private readonly List<BasicToken> _tokens = [];
    private int _offset;
    private int _line = 1;
    private int _column = 1;

    public IReadOnlyList<BasicToken> Lex()
    {
        while (_offset < source.Length)
        {
            var current = source[_offset];
            if (current is ' ' or '\t' or '\r')
            {
                Advance();
                continue;
            }

            if (current == '\n')
            {
                _tokens.Add(new(BasicTokenKind.NewLine, "\n", _line, _column));
                Advance();
                _line++;
                _column = 1;
                continue;
            }

            if (current == '\'')
            {
                while (_offset < source.Length && source[_offset] != '\n')
                    Advance();
                continue;
            }

            var line = _line;
            var column = _column;
            if (char.IsLetter(current) || current == '_')
            {
                var start = _offset;
                while (_offset < source.Length && (char.IsLetterOrDigit(source[_offset]) || source[_offset] == '_'))
                    Advance();
                _tokens.Add(new(BasicTokenKind.Identifier, source[start.._offset], line, column));
                continue;
            }

            if (char.IsDigit(current))
            {
                var start = _offset;
                while (_offset < source.Length && (char.IsDigit(source[_offset]) || source[_offset] == '.'))
                    Advance();
                _tokens.Add(new(BasicTokenKind.Number, source[start.._offset], line, column));
                continue;
            }

            if (current == '.')
            {
                _tokens.Add(new(BasicTokenKind.Dot, ".", line, column));
                Advance();
                continue;
            }

            if (current == '"')
            {
                Advance();
                var value = new System.Text.StringBuilder();
                var terminated = false;
                while (_offset < source.Length)
                {
                    if (source[_offset] == '\n')
                        throw new BasicDiagnosticException("Unterminated string literal", line, column);
                    if (source[_offset] == '"' && _offset + 1 < source.Length && source[_offset + 1] == '"')
                    {
                        value.Append('"');
                        Advance();
                        Advance();
                    }
                    else if (source[_offset] == '"')
                    {
                        Advance();
                        terminated = true;
                        break;
                    }
                    else
                    {
                        value.Append(source[_offset]);
                        Advance();
                    }
                }
                if (!terminated)
                    throw new BasicDiagnosticException("Unterminated string literal", line, column);
                _tokens.Add(new(BasicTokenKind.String, value.ToString(), line, column));
                continue;
            }

            var kind = current switch
            {
                '(' => BasicTokenKind.LeftParen,
                ')' => BasicTokenKind.RightParen,
                ',' => BasicTokenKind.Comma,
                '.' => BasicTokenKind.Dot,
                _ => BasicTokenKind.Operator
            };
            if ("+-*/\\&=<>".Contains(current))
            {
                var op = current.ToString();
                Advance();
                if (_offset < source.Length && (op == "<" && source[_offset] is '=' or '>' || op == ">" && source[_offset] == '='))
                {
                    op += source[_offset];
                    Advance();
                }
                _tokens.Add(new(BasicTokenKind.Operator, op, line, column));
                continue;
            }
            if (kind is BasicTokenKind.LeftParen or BasicTokenKind.RightParen or BasicTokenKind.Comma or BasicTokenKind.Dot)
            {
                _tokens.Add(new(kind, current.ToString(), line, column));
                Advance();
                continue;
            }

            throw new BasicDiagnosticException($"Unexpected character '{current}'", line, column);
        }

        _tokens.Add(new(BasicTokenKind.End, "", _line, _column));
        return _tokens;
    }

    private void Advance()
    {
        _offset++;
        _column++;
    }
}
