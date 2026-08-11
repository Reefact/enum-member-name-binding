using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using System.Diagnostics.CodeAnalysis;

using DiagnosticCatalog.NetAnalyzers;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// A declared public name is matched before an unannotated member's C# name, and case-sensitively,
/// while the C# names themselves are matched case-insensitively. A collision therefore leaves the
/// shadowed member answering to every casing of its name except the declared spelling — which must
/// be refused, whatever the casing of the declared name.
/// </summary>
/// <remarks>
/// Two <em>unannotated</em> members colliding the same way is the other half, and the answer is the
/// opposite one: it is not an ambiguity to refuse, because <c>System.Text.Json</c> resolves it — the
/// exact spelling wins, and only a casing matching none of them exactly falls back. Refusing it here
/// would make a registered enum stricter than the same enum left alone; dropping one of the two, as
/// this did, made it answer a word differently from the request body.
/// </remarks>
public sealed class ShadowedMemberTests {

    public enum ExactCasing {

        [JsonStringEnumMemberName("Blue")] Red,
        Blue

    }

    public enum LowerCasing {

        [JsonStringEnumMemberName("blue")] Red,
        Blue

    }

    public enum UpperCasing {

        [JsonStringEnumMemberName("BLUE")] Red,
        Blue

    }

    public enum NoCollision {

        [JsonStringEnumMemberName("crimson")] Red,
        Blue

    }

    /// <summary>Two unannotated members differing only by case, the uppercase one declared first.</summary>
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum UpperDeclaredFirst {

        [JsonStringEnumMemberName("one")] One = 1,
        Read = 2,
        read = 3

    }

    /// <summary>The same pair the other way round, which declaration order alone would resolve differently.</summary>
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum LowerDeclaredFirst {

        [JsonStringEnumMemberName("one")] One = 1,
        read = 3,
        Read = 2

    }

    /// <summary>
    /// Declaration order and <c>Enum.GetNames</c> order disagree here, because the lowercase member
    /// holds the lower value — so the casing that matches neither exactly separates the two rules.
    /// </summary>
    [SuppressMessage(NetAnalyzersRule.CA1708.Category, NetAnalyzersRule.CA1708.Id, Justification = SuppressionJustification.CA1708.TheShapeUnderTest)]
    public enum LowerHoldsTheLowerValue {

        [JsonStringEnumMemberName("one")] One = 9,
        Read = 5,
        read = 2

    }

    public static TheoryData<Type> Shadowing => new() { typeof(ExactCasing), typeof(LowerCasing), typeof(UpperCasing) };

    public static TheoryData<Type> CaseOnly => new() { typeof(UpperDeclaredFirst), typeof(LowerDeclaredFirst), typeof(LowerHoldsTheLowerValue) };

    [Theory]
    [MemberData(nameof(Shadowing))]
    public void a_shadowed_member_is_refused_whatever_the_casing(Type enumType) {
        EnumContractException exception = Check.ThatCode(() => EnumContract.For(enumType)).Throws<EnumContractException>().Value;

        Check.That(exception.EnumType).IsEqualTo(enumType);
        Check.That(exception.Problems).HasOneElementOnly();
        string problem = exception.Problems.Single();
        Check.That(problem).Contains("'Red'");
        Check.That(problem).Contains("'Blue'");
        Check.That(problem).Contains("casing");
    }

    [Fact]
    public void a_public_name_that_collides_with_nothing_is_accepted() {
        EnumContract contract = EnumContract.For(typeof(NoCollision));

        Check.That(contract.TryParse("crimson", out object? red)).IsTrue();
        Check.That(red).IsEqualTo(NoCollision.Red);
        Check.That(contract.TryParse("Blue", out object? blue)).IsTrue();
        Check.That(blue).IsEqualTo(NoCollision.Blue);
    }

    [Theory]
    [MemberData(nameof(Shadowing))]
    public void the_application_refuses_to_start(Type enumType) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        EnumContractException exception = Check.ThatCode(() => builder.Services.AddControllers().AddEnumMemberNameBinding(options => options.EnumTypes.Add(enumType)))
                                               .Throws<EnumContractException>().Value;

        Check.That(exception.EnumType).IsEqualTo(enumType);
    }

    /// <summary>
    /// Every casing resolves to whatever <c>System.Text.Json</c> resolves it to, the serializer being
    /// the oracle rather than a hand-written expectation.
    /// </summary>
    /// <remarks>
    /// The tokens are chosen so that each rule is separated from the next. <c>Read</c> and
    /// <c>read</c> name a member exactly and must reach that member and not the other, which is what
    /// a single case-insensitive dictionary could not do. <c>READ</c> and <c>rEaD</c> name none of
    /// them exactly and must reach the one the serializer picks. <c>one</c> is a declared name and
    /// <c>ONE</c> is that name miscased, which must stay refused — a fallback that reached it would
    /// make declared names case-insensitive, which they are not. <c>One</c> is the C# name of an
    /// annotated member, which the attribute replaced rather than added to.
    /// </remarks>
    [Theory]
    [MemberData(nameof(CaseOnly))]
    [SuppressMessage(NetAnalyzersRule.CA1062.Category, NetAnalyzersRule.CA1062.Id, Justification = SuppressionJustification.CA1062.ArgumentSuppliedByTheFramework)]
    public void a_casing_resolves_to_the_member_system_text_json_resolves_it_to(Type enumType) {
        EnumContract contract = EnumContract.For(enumType);
        JsonSerializerOptions oracle = OracleFor(enumType);

        List<string> divergences = [];

        foreach (string token in new[] { "Read", "read", "READ", "rEaD", "one", "ONE", "One" }) {
            object? expected = ReadWithSystemTextJson(token, enumType, oracle);
            object? actual   = contract.TryParse(token, out object? parsed) ? parsed : null;

            if (!Equals(expected, actual)) {
                divergences.Add($"'{token}': System.Text.Json reads {Show(expected)}, this library reads {Show(actual)}");
            }
        }

        Check.WithCustomMessage($"{enumType.Name} diverges on {divergences.Count} token(s):{Environment.NewLine}" + string.Join(Environment.NewLine, divergences))
             .That(divergences).IsEmpty();
    }

    /// <summary>Neither member is lost: each is named by something, and by something different.</summary>
    [Theory]
    [MemberData(nameof(CaseOnly))]
    [SuppressMessage(NetAnalyzersRule.CA1062.Category, NetAnalyzersRule.CA1062.Id, Justification = SuppressionJustification.CA1062.ArgumentSuppliedByTheFramework)]
    public void neither_of_the_two_members_is_unreachable(Type enumType) {
        EnumContract contract = EnumContract.For(enumType);

        Check.That(contract.TryParse("Read", out object? upper)).IsTrue();
        Check.That(contract.TryParse("read", out object? lower)).IsTrue();

        Check.WithCustomMessage("'Read' and 'read' name two different members, so they cannot read as one value.")
             .That(upper).IsNotEqualTo(lower);
    }

    /// <summary>
    /// Which spelling the shadowed member loses, measured against the serializer rather than stated.
    /// </summary>
    /// <remarks>
    /// It loses the declared one, and that is not always its own. Both messages this rule carries —
    /// the analyzer's and <c>EnumContractException</c>'s — used to say the shadowed member was
    /// "only reachable through a different casing", which is true of <see cref="ExactCasing" /> and
    /// false of <see cref="LowerCasing" />: there <c>Blue</c> still answers to <c>Blue</c>, and it is
    /// <c>blue</c> it loses. Half the shapes the rule reports were told the opposite of what happens.
    /// <para>
    /// The oracle has to be <c>System.Text.Json</c>, because this package refuses both shapes outright
    /// — there is no contract to ask. That is also why the claim went unchecked for so long: a message
    /// describing a shape nothing can build is a sentence no test was reaching for.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(typeof(ExactCasing), "Blue", "Red (0)", "the declared spelling reaches the annotated member")]
    [InlineData(typeof(ExactCasing), "blue", "Blue (1)", "every other casing reaches the shadowed one")]
    [InlineData(typeof(ExactCasing), "BLUE", "Blue (1)", "including the upper one")]
    [InlineData(typeof(LowerCasing), "blue", "Red (0)", "the declared spelling reaches the annotated member")]
    [InlineData(typeof(LowerCasing), "Blue", "Blue (1)", "and here that leaves the shadowed member its own name")]
    [InlineData(typeof(LowerCasing), "BLUE", "Blue (1)", "along with every other casing")]
    [SuppressMessage(NetAnalyzersRule.CA1062.Category, NetAnalyzersRule.CA1062.Id, Justification = SuppressionJustification.CA1062.ArgumentSuppliedByTheFramework)]
    public void the_shadowed_member_loses_the_declared_spelling_and_no_other(Type enumType, string token, string expected, string because) {
        string actual = Show(ReadWithSystemTextJson(token, enumType, OracleFor(enumType)));

        Check.WithCustomMessage($"'{token}' on {enumType.Name}: {because}.").That(actual).IsEqualTo(expected);
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

    private static string Show(object? value) {
        return value is null ? "nothing" : $"{value} ({Convert.ToInt64(value, CultureInfo.InvariantCulture)})";
    }

    /// <summary>
    /// The collision is an ambiguity, not a policy choice, so opting into partial contracts does not
    /// make it acceptable.
    /// </summary>
    [Fact]
    public void allowing_partial_contracts_does_not_make_a_shadowed_member_acceptable() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        Check.ThatCode(() => builder.Services.AddControllers().AddEnumMemberNameBinding(options => { options.EnumTypes.Add(typeof(LowerCasing)); options.AllowPartialContracts = true; }))
             .Throws<EnumContractException>();
    }

}
