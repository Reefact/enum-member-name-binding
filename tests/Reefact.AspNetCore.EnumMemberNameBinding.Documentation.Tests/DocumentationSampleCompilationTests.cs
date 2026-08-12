namespace Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Tests;

/// <summary>
/// The compile contract: every C# sample a consumer reads is real code that binds against the
/// shipped packages.
/// </summary>
/// <remarks>
/// <para>
/// Documentation rots in a way tests do not, because nothing executes it. A renamed option, an
/// extension method that moved namespace, a sample written from memory and never run — all three
/// read perfectly and all three are wrong, and the person who finds out is a newcomer who concludes
/// the library is broken. This makes the samples answer to the compiler.
/// </para>
/// <para>
/// A sample that cannot be code — a fragment of a call chain shown to point at one line — opts out
/// with <c>&lt;!-- emn:skip --&gt;</c> above its fence. The opt-out is visible in the page's source
/// and compared against the French twin, so it cannot be used quietly.
/// </para>
/// </remarks>
public sealed class DocumentationSampleCompilationTests {

    /// <summary>
    /// The scaffolding before the samples, because a suite whose own wrappers do not compile reports
    /// every page as broken and none of it is the pages' doing. First that the harness works, then
    /// what it says — the same order the coding-style job runs its checker and that checker's test.
    /// </summary>
    [Fact]
    public void the_scaffolding_a_sample_is_wrapped_in_compiles_on_its_own() {
        IReadOnlyList<string> errors = DocumentationSampleCompiler.ScaffoldingErrors();

        Check.WithCustomMessage($"the sample wrappers do not compile empty:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}")
             .That(errors).IsEmpty();
    }

    [Theory]
    [MemberData(nameof(DocumentationCorpus.PagesWithSamples), MemberType = typeof(DocumentationCorpus))]
    public void every_sample_compiles_against_the_shipped_packages(string page) {
        DocumentationPage documentation = DocumentationCorpus.Page(page);
        List<string>      failures      = [];

        foreach (CodeFence sample in documentation.Samples.Where(sample => !sample.Skipped)) {
            failures.AddRange(DocumentationSampleCompiler.ErrorsIn(documentation, sample));
        }

        Check.WithCustomMessage($"{page} carries C# that does not compile:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}")
             .That(failures).IsEmpty();
    }

    /// <summary>
    /// An opt-out that is no longer needed is a failure too. Left behind, it silently exempts a
    /// sample that has since become compilable — and the next one written beside it inherits the
    /// habit.
    /// </summary>
    [Theory]
    [MemberData(nameof(DocumentationCorpus.PagesWithSamples), MemberType = typeof(DocumentationCorpus))]
    public void a_sample_that_opts_out_of_the_compile_contract_still_needs_to(string page) {
        DocumentationPage documentation = DocumentationCorpus.Page(page);

        foreach (CodeFence sample in documentation.Samples.Where(sample => sample.Skipped)) {
            Check.WithCustomMessage($"{page}:{sample.StartLine}: this sample carries <!-- emn:skip --> but compiles; drop the marker, here and in the twin page.")
                 .That(DocumentationSampleCompiler.ErrorsIn(documentation, sample)).Not.IsEmpty();
        }
    }

}
