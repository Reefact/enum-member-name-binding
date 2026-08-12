global using Xunit;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Analyzers.Tests;

/// <summary>Compiles a snippet in memory and returns what the analyzer reports on it.</summary>
internal static class AnalyzerHarness {

    private static readonly ImmutableArray<MetadataReference> References = LoadReferences();

    internal static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(string source) {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "AnalyzerHarness",
            [CSharpSyntaxTree.ParseText(source)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // A snippet that does not compile would make the analyzer results meaningless.
        Diagnostic[] compilationErrors = [.. compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)];
        Check.WithCustomMessage("the test snippet does not compile: " + string.Join("; ", compilationErrors.Select(d => d.ToString())))
             .That(compilationErrors).IsEmpty();

        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([new EnumContractAnalyzer()]);

        return [.. (await withAnalyzers.GetAnalyzerDiagnosticsAsync()).OrderBy(d => d.Location.SourceSpan.Start)];
    }

    internal static async Task<IReadOnlyList<string>> IdsAsync(string source) {
        return [.. (await AnalyzeAsync(source)).Select(d => d.Id)];
    }

    private static ImmutableArray<MetadataReference> LoadReferences() {
        string assemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        return [.. assemblies.Split(Path.PathSeparator)
                             .Where(static path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                             .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                             .Select(static group => (MetadataReference)MetadataReference.CreateFromFile(group.First()))];
    }

}
