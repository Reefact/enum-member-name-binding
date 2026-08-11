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
/// That paid a second time. This file once named one shape as deliberately absent — a <c>[Flags]</c>
/// enum whose two case-only members differ in how many bits they set, where the serializer breaks the
/// tie by bit count and not by <c>Enum.GetNames</c> order. Closing it cost four declarations and no
/// test at all: the corpus found the divergence on its own, on tokens nobody would have thought to
/// write down.
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

    /// <summary>
    /// Two unannotated members differing only by case, on a <c>[Flags]</c> enum, setting a different
    /// number of bits — the shape this file used to name as a defect it did not cover.
    /// </summary>
    /// <remarks>
    /// The serializer holds a <c>[Flags]</c> enum's members with the most bits first and an ordinary
    /// enum's in <c>Enum.GetNames</c> order, so a token matching neither spelling exactly resolves
    /// differently on the two. Declared here in both directions, because getting the order right by
    /// accident is exactly what one direction cannot tell apart.
    /// </remarks>
    [Flags]
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum FlagsCasePairFewBitsFirst {

        Read = 1,
        read = 3,
        Write = 4

    }

    /// <summary>The same, with the composite declared first.</summary>
    [Flags]
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum FlagsCasePairManyBitsFirst {

        Read = 3,
        read = 1,
        Write = 4

    }

    /// <summary>
    /// A negative member on a <c>[Flags]</c> enum, where the bit count is taken over the widened
    /// value: <c>-128</c> sets one bit of the <c>sbyte</c> and fifty-seven of the <c>ulong</c>, and
    /// the serializer counts fifty-seven.
    /// </summary>
    [Flags]
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum FlagsCasePairSigned : sbyte {

        Read = -128,
        read = 3

    }

    /// <summary>Tied on bit count, so only the <c>GetNames</c> order is left to decide.</summary>
    [Flags]
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum FlagsCasePairTiedOnBits {

        read = 6,
        Read = 3

    }

    public static TheoryData<Type> Shapes => new() {
        typeof(DeclaredBesideACasePair), typeof(FlagsDeclaredBesideACasePair), typeof(CasePairAlone),
        typeof(FullyDeclared), typeof(PartiallyDeclared), typeof(FlagsAtoms),
        typeof(Aliases), typeof(SignedValues), typeof(UnsignedFlags),
        typeof(CommaInsideAName), typeof(CommaBesideCsharpNames),
        typeof(FlagsCasePairFewBitsFirst), typeof(FlagsCasePairManyBitsFirst),
        typeof(FlagsCasePairSigned), typeof(FlagsCasePairTiedOnBits)
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
    /// The corpus is the whole cross product and not merely a large number of tokens: a theory that
    /// generated nothing would pass in silence, which is the failure mode this whole file exists to
    /// avoid.
    /// </summary>
    /// <remarks>
    /// Derived from each shape's own vocabulary rather than a flat threshold, because a flat one
    /// answers the wrong question. Two hundred tokens is not evidence of coverage on an enum with
    /// eight names, and a two-member enum cannot reach a hundred however completely it is covered —
    /// which is what a flat 100 said about <see cref="FlagsCasePairSigned" />, a fixture that exists
    /// precisely because it is minimal.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Shapes))]
    [SuppressMessage(NetAnalyzersRule.CA1062.Category, NetAnalyzersRule.CA1062.Id, Justification = SuppressionJustification.CA1062.ArgumentSuppliedByTheFramework)]
    public void the_corpus_covers_every_casing_and_every_pair(Type enumType) {
        List<string> corpus = [.. Corpus(enumType)];
        int          pairs  = CasedNames(enumType).Length * CasedNames(enumType).Length;

        Check.WithCustomMessage($"{enumType.Name} has fewer tokens than it has ordered pairs, so the cross product is not there.")
             .That(corpus.Count).IsStrictlyGreaterThan(pairs);
        Check.That(corpus).ContainsNoDuplicateItem();
        Check.WithCustomMessage("the list path is the one a single value cannot exercise.")
             .That(corpus.Count(token => token.Contains(',', StringComparison.Ordinal))).IsGreaterOrEqualThan(pairs);
    }

    /// <summary>
    /// Every name the enum answers to — declared and C# alike — in four casings, alone and in ordered
    /// pairs, with the punctuation the serializer tolerates around them.
    /// </summary>
    private static HashSet<string> Corpus(Type enumType) {
        string[] cased = CasedNames(enumType);

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

    /// <summary>Every name the enum answers to, in every casing the corpus is built from.</summary>
    private static string[] CasedNames(Type enumType) {
        return [.. Vocabulary(enumType).SelectMany(Casings).Distinct(StringComparer.Ordinal)];
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
