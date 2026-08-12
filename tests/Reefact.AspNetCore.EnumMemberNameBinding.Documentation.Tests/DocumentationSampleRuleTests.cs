using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using Reefact.AspNetCore.EnumMemberNameBinding.Analyzers;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Tests;

/// <summary>
/// The rule contract: the documentation's own samples obey the analyzers this library ships.
/// </summary>
/// <remarks>
/// <para>
/// A library that ships six rules and then teaches the reader to break them has a credibility
/// problem, and the reader is the one who pays: samples get copied, and a sample carrying an
/// <c>EMN0005</c> is a defect propagated under the author's signature. Compiling a sample proves it
/// binds; running the rules over it proves it is also the code this library asks people to write.
/// </para>
/// <para>
/// The mistake still has to be shown — a page that only ever shows correct code cannot teach anyone
/// to recognise the wrong one. A sample declares the rules it means to trip with
/// <c>&lt;!-- emn:allow=EMN0001 --&gt;</c> above its fence, and an allowance that does NOT fire
/// fails too: a page saying "this is what EMN0001 looks like", above code that no longer trips
/// EMN0001, has quietly stopped being an example.
/// </para>
/// </remarks>
public sealed class DocumentationSampleRuleTests {

    [Theory]
    [MemberData(nameof(DocumentationCorpus.PagesWithSamples), MemberType = typeof(DocumentationCorpus))]
    public async Task every_sample_is_the_code_this_library_asks_for(string page) {
        DocumentationPage documentation = DocumentationCorpus.Page(page);
        List<string>      failures      = [];

        foreach (CodeFence sample in documentation.Samples.Where(sample => !sample.Skipped)) {
            if (DocumentationSampleCompiler.ShapeOf(sample) is not { } shape) { continue; }

            IReadOnlyList<string> reported = await ReportedIdsAsync(sample, shape);

            failures.AddRange(reported.Except(sample.AllowedRuleIds, StringComparer.Ordinal).Distinct(StringComparer.Ordinal)
                                      .Select(id => $"{page}:{sample.StartLine}: this sample trips {id}, which it does not declare. Fix the sample, or mark it <!-- emn:allow={id} --> in both languages."));

            failures.AddRange(sample.AllowedRuleIds.Except(reported, StringComparer.Ordinal).Distinct(StringComparer.Ordinal)
                                    .Select(id => $"{page}:{sample.StartLine}: this sample declares it shows {id}, and no longer trips it. It has stopped being the example the page says it is."));
        }

        Check.WithCustomMessage($"{page}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}")
             .That(failures).IsEmpty();
    }

    /// <summary>
    /// What the shipped analyzer reports on the sample itself — never on the scaffolding around it,
    /// which is why the diagnostics are filtered to the sample's own syntax tree.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ReportedIdsAsync(CodeFence sample, SampleShape shape) {
        SampleCompilation compilation = DocumentationSampleCompiler.Compile(sample, shape);

        CompilationWithAnalyzers analysed = compilation.Compilation.WithAnalyzers([new EnumContractAnalyzer()]);

        return [.. (await analysed.GetAnalyzerDiagnosticsAsync())
                   .Where(diagnostic => diagnostic.Location.SourceTree == compilation.Tree)
                   .Select(diagnostic => diagnostic.Id)];
    }

}
