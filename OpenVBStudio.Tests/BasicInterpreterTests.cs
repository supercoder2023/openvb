using OpenVBStudio.Core.Language;

namespace OpenVBStudio.Tests;

public sealed class BasicInterpreterTests
{
    [Fact]
    public void ExecutesButtonProcedureWithArithmeticConditionsAndMessageBox()
    {
        const string source = """
            Private Sub Command1_Click()
                Dim total As Integer
                For index = 1 To 3
                    total = total + index
                Next index
                If total = 6 Then
                    MsgBox "sum=" & total
                Else
                    MsgBox "wrong"
                End If
            End Sub
            """;
        var messages = new List<string>();

        new BasicInterpreter().Execute(source, "Command1_Click", messages.Add);

        Assert.Equal(["sum=6"], messages);
    }

    [Fact]
    public void ExecutesFunctionsAndDoWhileLoops()
    {
        const string source = """
            Private Function Twice(value As Integer) As Integer
                Twice = value * 2
            End Function
            Private Sub Command1_Click()
                Dim count As Integer
                Do While count < 2
                    count = count + 1
                Loop
                MsgBox Twice(count)
            End Sub
            """;
        var messages = new List<string>();

        new BasicInterpreter().Execute(source, "Command1_Click", messages.Add);

        Assert.Equal(["4"], messages);
    }

    [Fact]
    public void ReportsSyntaxErrorWithLineAndColumn()
    {
        const string source = "Private Sub Button_Click()\n    MsgBox (1 + )\nEnd Sub";

        var exception = Assert.Throws<BasicDiagnosticException>(() => new BasicCompiler().Compile(source));

        Assert.Equal(2, exception.Line);
        Assert.True(exception.Column > 1);
        Assert.Contains("line 2, column", exception.Message);
    }

    [Fact]
    public void ExecutesPostTestDoLoopAtLeastOnce()
    {
        const string source = """
            Private Sub Command1_Click()
                Dim count As Integer
                Do
                    count = count + 1
                Loop While count < 1
                MsgBox count
            End Sub
            """;
        var messages = new List<string>();

        new BasicInterpreter().Execute(source, "Command1_Click", messages.Add);

        Assert.Equal(["1"], messages);
    }

    [Fact]
    public void ShowsNamedFormThroughHostCallback()
    {
        const string source = """
            Private Sub Command1_Click()
                Form2.Show
            End Sub
            """;
        var shownForms = new List<string>();

        new BasicInterpreter().Execute(source, "Command1_Click", showForm: shownForms.Add);

        Assert.Equal(["Form2"], shownForms);
    }

    [Fact]
    public void ReadsTextBoxTextInMessageBoxExpression()
    {
        const string source = """
            Private Sub Command1_Click()
                MsgBox Text1.Text
            End Sub
            """;
        var messages = new List<string>();

        new BasicInterpreter().Execute(
            source,
            "Command1_Click",
            messages.Add,
            getControlProperty: (controlName, propertyName) =>
            {
                Assert.Equal("Text1", controlName);
                Assert.Equal("Text", propertyName);
                return "Hello from the textbox";
            });

        Assert.Equal(["Hello from the textbox"], messages);
    }
}
