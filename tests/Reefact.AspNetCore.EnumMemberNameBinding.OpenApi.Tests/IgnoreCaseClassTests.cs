using System.Globalization;

namespace Reefact.AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// The class the pattern writes for an unannotated member's name must admit exactly the characters
/// the binder treats as equal, over every <see cref="char" /> rather than over a sample.
/// </summary>
/// <remarks>
/// The pattern used to write a character's two case forms and call that the class. It is not one, and
/// the gap ran both ways — five code points advertised as interchangeable that the server refuses,
/// and seventy-nine equalities the server honours that the document excluded. Neither is visible from
/// a name spelled in ASCII, which is every name in the rest of this suite.
/// </remarks>
public sealed class IgnoreCaseClassTests {

    /// <summary>
    /// The five the old rule was too wide on, kept by code point so a regression is legible. Each is
    /// a character whose lower form the binder does <em>not</em> accept in its place: <c>KELVIN
    /// SIGN</c> lower-cases to <c>k</c>, and <c>OrdinalIgnoreCase</c> still refuses the two as equal.
    /// </summary>
    public static TheoryData<char, char> OverPromised => new() {
        { 'ϴ', 'θ' },
        { 'ẞ', 'ß' },
        { 'Ω', 'ω' },
        { 'K', 'k' },
        { 'Å', 'å' }
    };

    /// <summary>
    /// Two of the seventy-nine it was too narrow on, where two characters are equal without either
    /// being the other's case form: MICRO SIGN against GREEK SMALL MU, and the title-case DZ family.
    /// </summary>
    public static TheoryData<char, char> UnderPromised => new() {
        { 'µ', 'μ' },
        { 'Ǆ', 'ǅ' }
    };

    /// <summary>
    /// The rule the implementation is built on: <c>OrdinalIgnoreCase</c> equality is exactly equality
    /// of <see cref="char.ToUpperInvariant(char)" />. Asserted rather than assumed, because the whole
    /// grouping rests on it.
    /// </summary>
    [Fact]
    public void ordinal_ignore_case_is_equality_of_the_upper_form() {
        List<string> divergences = [];

        for (int code = 0; code <= char.MaxValue; code++) {
            char first = (char)code;

            foreach (char second in Candidates(first)) {
                bool equal     = string.Equals(first.ToString(), second.ToString(), StringComparison.OrdinalIgnoreCase);
                bool sameUpper = char.ToUpperInvariant(first) == char.ToUpperInvariant(second);

                if (equal != sameUpper) { divergences.Add($"U+{code:X4} vs U+{(int)second:X4}: equal={equal}, sameUpper={sameUpper}"); }
            }
        }

        Check.WithCustomMessage($"the grouping rule fails on {divergences.Count} pair(s): {string.Join(", ", divergences.Take(8))}")
             .That(divergences).IsEmpty();
    }

    /// <summary>
    /// The class emitted for each character admits exactly the characters the binder accepts there.
    /// </summary>
    [Fact]
    public void the_pattern_admits_exactly_what_the_binder_accepts() {
        List<string> divergences = [];

        for (int code = 0; code <= char.MaxValue; code++) {
            char   character = (char)code;
            string written   = Written(character);

            foreach (char other in Candidates(character)) {
                bool accepted = string.Equals(character.ToString(), other.ToString(), StringComparison.OrdinalIgnoreCase);
                bool admitted = written.Length > 1 ? written.Contains(other, StringComparison.Ordinal) : written[0] == other;

                if (accepted != admitted) { divergences.Add($"name has U+{code:X4}, U+{(int)other:X4}: accepted={accepted}, admitted={admitted}"); }
            }
        }

        Check.WithCustomMessage($"the class and the binder disagree on {divergences.Count} pair(s): {string.Join(", ", divergences.Take(8))}")
             .That(divergences).IsEmpty();
    }

    /// <summary>
    /// No emitted class <em>contains</em> a character a class would have to escape, which is what
    /// lets the group be written between brackets as it stands.
    /// </summary>
    /// <remarks>
    /// The interior, not the written form: every class ends in <c>]</c>, so asking whether the string
    /// carries one answers yes for all two thousand of them. This test failed that way first, which is
    /// the cheap direction — the expensive one would have been a green check on the wrong substring.
    /// </remarks>
    [Fact]
    public void no_class_contains_a_character_a_class_would_have_to_escape() {
        List<string> offenders = [];

        foreach (KeyValuePair<char, string> entry in EnumMemberNameSchemaTransformer.AnyCasing.Value) {
            string interior = entry.Value[1..^1];

            foreach (char dangerous in @"\]^-") {
                if (interior.Contains(dangerous, StringComparison.Ordinal)) {
                    offenders.Add($"U+{(int)entry.Key:X4} writes '{entry.Value}', whose group carries '{dangerous}'");
                }
            }
        }

        Check.That(offenders).IsEmpty();
    }

    /// <summary>
    /// A character alone in its group stays outside a class, so a hyphen is never read as a range.
    /// </summary>
    [Fact]
    public void a_character_with_no_equal_is_not_written_as_a_class() {
        foreach (char alone in "-_0123456789") {
            Check.WithCustomMessage($"'{alone}' was given a class.")
                 .That(EnumMemberNameSchemaTransformer.AnyCasing.Value.ContainsKey(alone)).IsFalse();
        }
    }

    /// <summary>The ASCII shape the documentation shows is unchanged by the wider rule.</summary>
    [Theory]
    [InlineData('d', "[Dd]")]
    [InlineData('E', "[Ee]")]
    [InlineData('k', "[Kk]")]
    public void an_ascii_letter_still_writes_its_two_forms_and_nothing_else(char letter, string expected) {
        Check.That(EnumMemberNameSchemaTransformer.AnyCasing.Value[letter]).IsEqualTo(expected);
    }

    /// <summary>
    /// A member named with one of the five no longer advertises the character the binder refuses.
    /// </summary>
    [Theory]
    [MemberData(nameof(OverPromised))]
    public void a_code_point_the_binder_refuses_is_no_longer_advertised(char inName, char refused) {
        Check.WithCustomMessage($"{Describe(inName)} and {Describe(refused)} are equal after all, so this is not the shape under test.")
             .That(string.Equals(inName.ToString(), refused.ToString(), StringComparison.OrdinalIgnoreCase)).IsFalse();

        Check.WithCustomMessage($"{Describe(inName)} still advertises {Describe(refused)}, which the binder refuses.")
             .That(Written(inName).Contains(refused, StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>And a member named with one of the seventy-nine now admits the equal the binder honours.</summary>
    [Theory]
    [MemberData(nameof(UnderPromised))]
    public void a_code_point_the_binder_accepts_is_now_advertised(char inName, char accepted) {
        Check.That(string.Equals(inName.ToString(), accepted.ToString(), StringComparison.OrdinalIgnoreCase)).IsTrue();

        Check.WithCustomMessage($"{Describe(inName)} excludes {Describe(accepted)}, which the binder accepts.")
             .That(Written(inName).Contains(accepted, StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>What the pattern writes for one character: its class, or the character itself.</summary>
    private static string Written(char character) {
        return EnumMemberNameSchemaTransformer.AnyCasing.Value.TryGetValue(character, out string? group) ? group : character.ToString();
    }

    /// <summary>
    /// The characters worth comparing <paramref name="character" /> against: itself, its two case
    /// forms, and everything sharing its group. Every char against every char would be four billion
    /// comparisons for the same answer, since equality outside the group is what the first test
    /// establishes cannot happen.
    /// </summary>
    private static IEnumerable<char> Candidates(char character) {
        yield return character;
        yield return char.ToUpperInvariant(character);
        yield return char.ToLowerInvariant(character);

        if (!EnumMemberNameSchemaTransformer.AnyCasing.Value.TryGetValue(character, out string? group)) { yield break; }

        for (int index = 1; index < group.Length - 1; index++) { yield return group[index]; }
    }

    private static string Describe(char character) {
        return "U+" + ((int)character).ToString("X4", CultureInfo.InvariantCulture);
    }

}
