using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using System.Diagnostics.CodeAnalysis;

using DiagnosticCatalog.NetAnalyzers;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// Reading a value must accept exactly what <c>System.Text.Json</c> accepts, and resolve it to the
/// same member — the counterpart of <see cref="FormattingParityTests" />, which holds the same for
/// writing. The oracle is the serializer itself, never a hand-written expectation.
/// </summary>
/// <remarks>
/// The corpus is <em>derived from each enum's own names</em> rather than listed, and that is the
/// whole design. A hand-listed corpus tests the shapes its author thought of, which is how the
/// exact-spelling rule came to be applied to comma-separated lists: every example written beside
/// that change was a single value, so the list path was never compared to the serializer at all and
/// a regression read as a fix. Adding a fixture below now costs nothing and buys its full cross
/// product — every casing of every name, every ordered pair, and the punctuation around them.
/// <para>
/// One shape is deliberately absent: a <c>[Flags]</c> enum whose two case-only members differ in how
/// many bits they set. The serializer breaks a case-insensitive tie by <c>Enum.GetNames</c> order on
/// an ordinary enum and by bit count on a <c>[Flags]</c> one, and this library applies the first rule
/// to both. That is a separate defect, reported and open; it is named here so the boundary of this
/// corpus is visible rather than merely unexercised.
/// </para>
/// </remarks>
public sealed class ReadParityTests {

    /// <summary>The shape the regression was found on: a declared name beside a case-only pair.</summary>
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum DeclaredBesideACasePair {

        [JsonStringEnumMemberName("one")] One = 1,
        Read = 2,
        read = 4

    }

    /// <summary>The same, carrying <c>[Flags]</c>: both members set one bit, so the tie-break agrees.</summary>
    [Flags]
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum FlagsDeclaredBesideACasePair {

        [JsonStringEnumMemberName("one")] One = 1,
        Read = 2,
        read = 4

    }

    /// <summary>A case-only pair with no declared name anywhere.</summary>
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum CasePairAlone {

        Read = 1,
        read = 2,
        Write = 4

    }

    /// <summary>Every member annotated: no C# name is part of the vocabulary at all.</summary>
    public enum FullyDeclared {

        [JsonStringEnumMemberName("available")]    Available    = 1,
        [JsonStringEnumMemberName("out_of_stock")] OutOfStock   = 2,
        [JsonStringEnumMemberName("discontinued")] Discontinued = 4

    }

    /// <summary>Partly annotated, with no casing collision — the ordinary partial contract.</summary>
    public enum PartiallyDeclared {

        [JsonStringEnumMemberName("one")] One = 1,
        Two = 2,
        Three = 4

    }

    [Flags]
    public enum FlagsAtoms {

        [JsonStringEnumMemberName("read")]   Read   = 1,
        [JsonStringEnumMemberName("write")]  Write  = 2,
        [JsonStringEnumMemberName("delete")] Delete = 4

    }

    /// <summary>Two members on one value, which the read path must resolve to that value from either name.</summary>
    public enum Aliases {

        [JsonStringEnumMemberName("first")]  First  = 1,
        [JsonStringEnumMemberName("uno")]    Uno    = 1,
        [JsonStringEnumMemberName("second")] Second = 2

    }

    /// <summary>Negative and zero values, which the widen-OR-narrow arithmetic can lose.</summary>
    public enum SignedValues {

        [JsonStringEnumMemberName("negative")] Negative = -1,
        [JsonStringEnumMemberName("zero")]     Zero     = 0,
        [JsonStringEnumMemberName("positive")] Positive = 1

    }

    [Flags]
    public enum UnsignedFlags : ulong {

        [JsonStringEnumMemberName("low")]  Low  = 1,
        [JsonStringEnumMemberName("high")] High = 1UL << 63

    }

    /// <summary>
    /// A declared name carrying a comma, beside the two names it reads as a combination of — the
    /// shape where looking the whole value up first and splitting first give different answers.
    /// </summary>
    /// <remarks>
    /// Legal off <c>[Flags]</c>, and the corpus is what says so rather than an expectation written
    /// here: <c>"a,b"</c> is the member of that name, <c>"a, b"</c> is <c>a | b</c> because no name
    /// spells it with a space, and every casing of both is compared to the serializer. The
    /// <c>[Flags]</c> counterpart cannot be written down at all — <c>System.Text.Json</c> refuses to
    /// build a converter for it, which is what <c>EMN0004</c> reports and
    /// <c>ContractValidationTests</c> pins.
    /// </remarks>
    public enum CommaInsideAName {

        [JsonStringEnumMemberName("a,b")] Ab = 4,
        [JsonStringEnumMemberName("a")]   A  = 1,
        [JsonStringEnumMemberName("b")]   B  = 2

    }

    /// <summary>The same comma, on an enum whose other members keep their C# names.</summary>
    public enum CommaBesideCsharpNames {

        [JsonStringEnumMemberName("news,world")] NewsWorld = 1,
        Sport = 2

    }

    public static TheoryData<Type> Shapes => new() {
        typeof(DeclaredBesideACasePair), typeof(FlagsDeclaredBesideACasePair), typeof(CasePairAlone),
        typeof(FullyDeclared), typeof(PartiallyDeclared), typeof(FlagsAtoms),
        typeof(Aliases), typeof(SignedValues), typeof(UnsignedFlags),
        typeof(CommaInsideAName), typeof(CommaBesideCsharpNames)
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    [SuppressMessage(NetAnalyzersRule.CA1062.Category, NetAnalyzersRule.CA1062.Id, Justification = SuppressionJustification.CA1062.ArgumentSuppliedByTheFramework)]
    public void every_token_reads_exactly_as_system_text_json_reads_it(Type enumType) {
        EnumContract contract = EnumContract.For(enumType);
        JsonSerializerOptions oracle = OracleFor(enumType);

        List<string> divergences = [];

        foreach (string token in Corpus(enumType)) {
            string expected = Show(ReadWithSystemTextJson(token, enumType, oracle));
            string actual   = Show(contract.TryParse(token, out object? parsed) ? parsed : null);

            if (!string.Equals(expected, actual, StringComparison.Ordinal)) {
                divergences.Add($"'{token}': System.Text.Json reads {expected}, this library reads {actual}");
            }
        }

        Check.WithCustomMessage($"{enumType.Name} diverges on {divergences.Count} token(s):{Environment.NewLine}" + string.Join(Environment.NewLine, divergences.Take(20)))
             .That(divergences).IsEmpty();
    }

    /// <summary>
    /// The corpus is large enough to be worth asserting on: a theory that generated nothing would
    /// pass in silence, which is the failure mode this whole file exists to avoid.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    [SuppressMessage(NetAnalyzersRule.CA1062.Category, NetAnalyzersRule.CA1062.Id, Justification = SuppressionJustification.CA1062.ArgumentSuppliedByTheFramework)]
    public void the_corpus_covers_every_casing_and_every_pair(Type enumType) {
        List<string> corpus = [.. Corpus(enumType)];

        Check.WithCustomMessage("a corpus this small would pass by not asking anything.")
             .That(corpus.Count).IsStrictlyGreaterThan(100);
        Check.That(corpus).ContainsNoDuplicateItem();
        Check.WithCustomMessage("the list path is the one a single value cannot exercise.")
             .That(corpus.Count(token => token.Contains(',', StringComparison.Ordinal))).IsStrictlyGreaterThan(50);
    }

    /// <summary>
    /// Every name the enum answers to — declared and C# alike — in four casings, alone and in ordered
    /// pairs, with the punctuation the serializer tolerates around them.
    /// </summary>
    private static HashSet<string> Corpus(Type enumType) {
        string[] names = [.. Vocabulary(enumType)];
        string[] cased = [.. names.SelectMany(Casings).Distinct(StringComparer.Ordinal)];

        HashSet<string> corpus = new(StringComparer.Ordinal);

        foreach (string one in cased) {
            corpus.Add(one);
            corpus.Add(" " + one);
            corpus.Add(one + " ");
            corpus.Add(one + ",");
            corpus.Add(one + ", ");
        }

        foreach (string left in cased) {
            foreach (string right in cased) {
                corpus.Add(left + "," + right);
                corpus.Add(left + ", " + right);
            }
        }

        // Shapes no name can produce, so the refusals are exercised too.
        foreach (string junk in new[] { "", " ", ",", ",x", "x,,y", "bogus", "0", "1", "-1", "999" }) {
            corpus.Add(junk);
        }

        return corpus;
    }

    /// <summary>The public names and the C# names both, since a partial contract answers to each.</summary>
    private static IEnumerable<string> Vocabulary(Type enumType) {
        return EnumContract.For(enumType).PublicNames.Concat(Enum.GetNames(enumType)).Distinct(StringComparer.Ordinal);
    }

    private static IEnumerable<string> Casings(string name) {
        yield return name;
        yield return name.ToUpperInvariant();
        yield return name.ToLowerInvariant();
        yield return Alternating(name);
    }

    private static string Alternating(string name) {
        StringBuilder alternating = new(name.Length);

        for (int index = 0; index < name.Length; index++) {
            char character = name[index];
            alternating.Append(index % 2 == 0 ? char.ToLowerInvariant(character) : char.ToUpperInvariant(character));
        }

        return alternating.ToString();
    }

    private static JsonSerializerOptions OracleFor(Type enumType) {
        Type converterType = typeof(JsonStringEnumConverter<>).MakeGenericType(enumType);

        return new JsonSerializerOptions {
            Converters = { (JsonConverter)Activator.CreateInstance(converterType, null, false)! }
        };
    }

    private static object? ReadWithSystemTextJson(string token, Type enumType, JsonSerializerOptions oracle) {
        try {
            return JsonSerializer.Deserialize(JsonSerializer.Serialize(token), enumType, oracle);
        } catch (JsonException) {
            return null;
        }
    }

    /// <summary>
    /// A stable numeric identity for a member, whatever the enum is backed by.
    /// </summary>
    /// <remarks>
    /// The <c>ulong</c> case is not hypothetical tidiness: this method read every value through
    /// <c>Convert.ToInt64</c> first, and the corpus answered with an <c>OverflowException</c> on a
    /// member at <c>1UL &lt;&lt; 63</c> — the harness failing before it could hide anything, which is
    /// the direction that costs nothing.
    /// </remarks>
    private static string Show(object? value) {
        if (value is null) { return "nothing"; }

        return Type.GetTypeCode(Enum.GetUnderlyingType(value.GetType())) switch {
            TypeCode.UInt64 => Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            _               => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
        };
    }

}
