using System.Collections.Immutable;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Tests;

/// <summary>
/// Turns a fenced sample into a real compilation, against the shipped packages and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// The references are the reader's references: the running runtime, the ASP.NET Core shared
/// framework, and the two packages this repository publishes. A sample therefore cannot compile
/// here by leaning on something only this repository has — if it binds, it binds in a consumer's
/// project too.
/// </para>
/// <para>
/// What the samples are NOT is compilation units. Documentation shows the line that matters: an
/// action without the controller around it, three statements without the <c>Main</c> around them,
/// an enum entry without the enum. So each sample is wrapped, and which wrapping it needs is
/// inferred by parsing — see <see cref="ShapeOf" />.
/// </para>
/// </remarks>
internal static partial class DocumentationSampleCompiler {

    private const string SampleNamespace = "Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Samples";

    /// <summary>
    /// The usings a reader would have at the top of the file they are pasting into. A page is free
    /// to show its own — the one in the front page's first sample is part of what it teaches — and
    /// a duplicate directive is a warning, which this suite does not read.
    /// </summary>
    private const string Prelude = """
                                   using System;
                                   using System.Collections.Generic;
                                   using System.Diagnostics.CodeAnalysis;
                                   using System.Linq;
                                   using System.Text.Json.Serialization;
                                   using System.Threading.Tasks;

                                   using Microsoft.AspNetCore.Builder;
                                   using Microsoft.AspNetCore.Http;
                                   using Microsoft.AspNetCore.Mvc;
                                   using Microsoft.AspNetCore.Routing;
                                   using Microsoft.Extensions.DependencyInjection;

                                   using Reefact.AspNetCore.EnumMemberNameBinding;
                                   using Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Tests.Fixtures;
                                   using static Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Samples.Ambient;

                                   """;

    /// <summary>
    /// The values a sample writes without introducing: <c>builder</c>, <c>args</c>, <c>links</c>,
    /// <c>context</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reached through <c>using static</c> rather than passed as parameters, which is what lets the
    /// front page's <c>var builder = WebApplication.CreateBuilder(args);</c> still declare its own:
    /// a local may shadow an imported member, and may not shadow a parameter.
    /// </para>
    /// <para>
    /// Injected as source rather than written in a file of this project, because the members have to
    /// be named the way the documentation names them — <c>builder</c>, not <c>Builder</c> — and no
    /// naming convention this repository enforces would let that through. Nothing reads them: a
    /// sample is compiled, never run.
    /// </para>
    /// </remarks>
    private const string AmbientSource = """
                                         using System;

                                         namespace Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Samples;

                                         internal static class Ambient {

                                             internal static string[] args => throw new NotSupportedException();
                                             internal static Microsoft.AspNetCore.Builder.WebApplicationBuilder builder => throw new NotSupportedException();
                                             internal static Microsoft.AspNetCore.Routing.LinkGenerator links => throw new NotSupportedException();
                                             internal static Microsoft.AspNetCore.Http.HttpContext context => throw new NotSupportedException();

                                         }
                                         """;

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly ImmutableArray<MetadataReference> References = LoadReferences();

    /// <summary>A using DIRECTIVE at the top of a sample, which no wrapping can keep in place.</summary>
    /// <remarks>
    /// Hoisted into the prelude, and replaced by a blank line so every later line keeps its number:
    /// a failure has to name the line of the page a maintainer can open. Deliberately narrow enough
    /// not to match <c>using var x = …;</c>, which is a statement and belongs where it was written.
    /// </remarks>
    [GeneratedRegex(@"^\s*using\s+(static\s+)?(?!var\b)[A-Za-z_][\w.]*(\s*=\s*[^;]+)?\s*;\s*$")]
    private static partial Regex UsingDirective();

    /// <summary>The compiler ERRORS a sample produces, already mapped back to the page.</summary>
    public static IReadOnlyList<string> ErrorsIn(DocumentationPage page, CodeFence fence) {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(fence);

        if (ShapeOf(fence) is not { } shape) { return [UnwrappableMessage(page, fence)]; }

        SampleCompilation sample = Compile(fence, shape);

        // Filtered to the sample's own tree, so a mistake in the scaffolding is never reported as a
        // line of the page. ScaffoldingErrors is what holds the scaffolding itself to account.
        return [.. sample.Compilation.GetDiagnostics()
                         .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Location.SourceTree == sample.Tree)
                         .Select(diagnostic => Describe(page, fence, diagnostic, sample.PrecedingLines))];
    }

    /// <summary>
    /// What the prelude, the ambient values and the wrappers produce on their own, with no sample in
    /// them. Anything here is this suite's own defect, and every page would fail on it.
    /// </summary>
    public static IReadOnlyList<string> ScaffoldingErrors() {
        List<string> errors = [];

        foreach (SampleShape shape in Enum.GetValues<SampleShape>()) {
            CodeFence empty = new(string.Empty, StartLine: 0, Skipped: false, [], string.Empty);

            errors.AddRange(Compile(empty, shape).Compilation.GetDiagnostics()
                                                 .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                                                 .Select(diagnostic => $"{shape}: {diagnostic.Id} {diagnostic.GetMessage()}"));
        }

        return errors;
    }

    /// <summary>Builds the compilation for one sample; the rule contract needs the compilation itself.</summary>
    public static SampleCompilation Compile(CodeFence fence, SampleShape shape) {
        ArgumentNullException.ThrowIfNull(fence);

        (string source, int precedingLines) = Wrap(fence, shape);

        SyntaxTree tree    = CSharpSyntaxTree.ParseText(source, ParseOptions, path: "sample.cs");
        SyntaxTree ambient = CSharpSyntaxTree.ParseText(AmbientSource, ParseOptions, path: "ambient.cs");

        CSharpCompilation compilation = CSharpCompilation.Create(
            "Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Sample",
            [tree, ambient],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        return new SampleCompilation(compilation, tree, precedingLines, shape);
    }

    /// <summary>
    /// The first wrapping the sample parses under, or <c>null</c> when none of them does.
    /// </summary>
    /// <remarks>
    /// The order is what makes this unambiguous, and it runs from the most complete shape to the
    /// least. A type declaration also parses inside a class, so <see cref="SampleShape.Declarations" />
    /// has to be asked first; an enum entry parses in neither of those; and a run of statements
    /// parses in none of the three. A sample that parses nowhere is a sample the reader cannot
    /// compile either, which is what <c>emn:skip</c> is for.
    /// </remarks>
    public static SampleShape? ShapeOf(CodeFence fence) {
        ArgumentNullException.ThrowIfNull(fence);

        foreach (SampleShape candidate in Enum.GetValues<SampleShape>()) {
            (string source, _) = Wrap(fence, candidate);

            if (ParsesAs(source, candidate)) { return candidate; }
        }

        return null;
    }

    /// <summary>
    /// Whether a wrapping holds. Syntax is most of the answer, and for
    /// <see cref="SampleShape.Declarations" /> it is not all of it: a method or a field parses
    /// perfectly well at namespace level and only fails later, with CS0116. Left to syntax alone,
    /// an action shown without its controller would be accepted as the first shape and then be
    /// reported as broken documentation — so that one shape is asked what it declared, not just
    /// whether it parsed.
    /// </summary>
    private static bool ParsesAs(string source, SampleShape shape) {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, ParseOptions);

        if (tree.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) { return false; }
        if (shape != SampleShape.Declarations) { return true; }

        return tree.GetRoot()
                   .DescendantNodes()
                   .OfType<FileScopedNamespaceDeclarationSyntax>()
                   .Single()
                   .Members.All(member => member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);
    }

    /// <summary>Renders a diagnostic as a message naming the page and line a maintainer can open.</summary>
    public static string Describe(DocumentationPage page, CodeFence fence, Diagnostic diagnostic, int precedingLines) {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(fence);
        ArgumentNullException.ThrowIfNull(diagnostic);

        int generatedLine = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;
        int pageLine      = fence.StartLine + (generatedLine - precedingLines);

        return $"{page.RelativePath}:{pageLine}: {diagnostic.Id} {diagnostic.GetMessage()}";
    }

    private static string UnwrappableMessage(DocumentationPage page, CodeFence fence) {
        (string source, int precedingLines) = Wrap(fence, SampleShape.Declarations);

        string first = CSharpSyntaxTree.ParseText(source, ParseOptions).GetDiagnostics()
                                       .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                                       .Select(diagnostic => Describe(page, fence, diagnostic, precedingLines))
                                       .FirstOrDefault("(no diagnostic)");

        return $"{page.RelativePath}:{fence.StartLine}: this sample parses as no C# this suite can wrap — as types, as members of a class or of an enum, or as statements. "
             + $"Either make it one of those, or mark it <!-- emn:skip --> above the fence, in both languages. First error: {first}";
    }

    private static (string Source, int PrecedingLines) Wrap(CodeFence fence, SampleShape shape) {
        (string hoisted, string body) = HoistUsings(fence.Content);

        string prefix = Prelude + hoisted + $"namespace {SampleNamespace};\n\n" + shape switch {
            SampleShape.Declarations => string.Empty,
            SampleShape.ClassMember  => "public sealed class DocumentationSampleController : ControllerBase {\n",
            SampleShape.EnumMember   => "public enum DocumentationSampleContract {\n",
            SampleShape.Statements   => "internal static class DocumentationSampleProgram {\n    internal static async Task RunAsync() {\n",
            _                        => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown sample shape.")
        };

        string suffix = shape switch {
            SampleShape.Declarations => string.Empty,
            SampleShape.ClassMember  => "\n}\n",
            SampleShape.EnumMember   => "\n}\n",
            SampleShape.Statements   => "\n    }\n}\n",
            _                        => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown sample shape.")
        };

        return (prefix + body + suffix, prefix.Count(character => character == '\n'));
    }

    private static (string Hoisted, string Body) HoistUsings(string content) {
        string[]     lines   = content.Split('\n');
        List<string> hoisted = [];

        for (int index = 0; index < lines.Length; index++) {
            if (lines[index].Trim().Length == 0) { continue; }
            if (!UsingDirective().IsMatch(lines[index])) { break; }

            hoisted.Add(lines[index].Trim());
            lines[index] = string.Empty;
        }

        return (hoisted.Count == 0 ? string.Empty : string.Join('\n', hoisted) + "\n\n", string.Join('\n', lines));
    }

    /// <summary>
    /// What this suite's own process is running on, minus what a reader would not have.
    /// </summary>
    /// <remarks>
    /// Everything loaded here is everything a consumer gets — the runtime, the ASP.NET Core shared
    /// framework, the two shipped packages and their dependencies — plus this assembly, which the
    /// samples need for the illustrative domain in <c>Fixtures</c>. What a consumer does NOT get is
    /// the machinery that runs this suite, so it is removed: left in, a sample could compile by
    /// naming <c>Check.That</c> or a Roslyn type and still be broken for everyone reading the page.
    /// </remarks>
    private static ImmutableArray<MetadataReference> LoadReferences() {
        // A local rather than a field: References is initialised by this method, and a static field
        // declared below it would still be null by the time it ran.
        string[] suiteOwnTooling = ["xunit", "NFluent", "Microsoft.CodeAnalysis", "Microsoft.TestPlatform", "Microsoft.VisualStudio.TestPlatform", "testhost", "coverlet"];

        string assemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        return [.. assemblies.Split(Path.PathSeparator)
                             .Where(static path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                             .GroupBy(static path => Path.GetFileNameWithoutExtension(path) ?? path, StringComparer.OrdinalIgnoreCase)
                             .Where(group => !suiteOwnTooling.Any(tool => group.Key.StartsWith(tool, StringComparison.OrdinalIgnoreCase)))
                             .Select(static group => (MetadataReference)MetadataReference.CreateFromFile(group.First()))];
    }

}

/// <summary>One sample, compiled: the compilation, and what a diagnostic in it means on the page.</summary>
/// <param name="Compilation">The whole compilation, sample and ambient values together.</param>
/// <param name="Tree">The sample's own syntax tree, so a diagnostic from the scaffolding can be told apart.</param>
/// <param name="PrecedingLines">How many generated lines sit above the sample's first line.</param>
/// <param name="Shape">The wrapping the sample was inferred to need.</param>
internal sealed record SampleCompilation(CSharpCompilation Compilation, SyntaxTree Tree, int PrecedingLines, SampleShape Shape);
