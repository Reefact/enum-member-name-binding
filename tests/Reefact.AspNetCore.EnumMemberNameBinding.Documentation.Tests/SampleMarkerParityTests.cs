namespace Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Tests;

/// <summary>
/// A marker is part of the page, so it is translated with it — or the exemption it carries applies
/// to one language and not the other.
/// </summary>
/// <remarks>
/// Both markers are exemptions: <c>emn:skip</c> from the compile contract, <c>emn:allow</c> from
/// the rule contract. An exemption that exists on the English page alone leaves the French sample
/// held to a contract it was never rewritten for — or, the other way round, quietly exempts a
/// French sample nobody argued for. Comparing them in order also catches the subtler case: the same
/// markers, moved onto different samples.
/// </remarks>
public sealed class SampleMarkerParityTests {

    [Theory]
    [MemberData(nameof(DocumentationCorpus.TranslationPairs), MemberType = typeof(DocumentationCorpus))]
    public void a_translation_carries_the_same_sample_markers(string english, string french) {
        string[] left  = MarkersOf(english);
        string[] right = MarkersOf(french);

        Check.WithCustomMessage($"{english} marks its samples {Show(left)} and {french} marks them {Show(right)}.")
             .That(right).IsEqualTo(left);
    }

    private static string[] MarkersOf(string page) {
        return [.. DocumentationCorpus.Page(page).Samples.Select(sample => sample.Marker.Length == 0 ? "-" : sample.Marker)];
    }

    private static string Show(string[] markers) {
        return markers.Length == 0 ? "(no C# sample)" : string.Join(' ', markers);
    }

}
