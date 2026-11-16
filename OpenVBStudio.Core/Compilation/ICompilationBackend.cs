using OpenVBStudio.Core.Projects;

namespace OpenVBStudio.Core.Compilation;

public interface ICompilationBackend
{
    string Name { get; }
    Task<CompilationResult> CompileAsync(ProjectDocument project, string outputPath, CancellationToken cancellationToken = default);
}

public sealed record CompilationDiagnostic(string Message, string? File = null, int? Line = null, int? Column = null);

public sealed record CompilationResult(bool Succeeded, string? OutputPath, IReadOnlyList<CompilationDiagnostic> Diagnostics);
