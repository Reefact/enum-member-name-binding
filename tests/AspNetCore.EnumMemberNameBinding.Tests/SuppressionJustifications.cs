using System.Diagnostics.CodeAnalysis;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// The reasons carried by this project's analyzer suppressions, addressed by the rule they excuse.
/// </summary>
/// <remarks>
/// Shaped after the rule catalogues the suppressions already reference, so a use site reads the same
/// way on both sides: <c>CodeStyleRule.IDE0028.Id</c> names the rule, and
/// <c>SuppressionJustification.IDE0028</c> names why it was silenced here.
///
/// Duplicated in the sibling test project rather than shared, matching how the packages treat their
/// own suppression wording: a reason is local to the code it excuses, and a type shared across
/// projects only to carry a string would outlive the reason it was written for.
///
/// Excluded from coverage for the reason given on the main package's copy: a class of constants has
/// no behaviour to measure, and a <c>const</c> initialised from another constant is nevertheless
/// counted as an executable line by Sonar's C# sensor — uncoverable by construction.
/// </remarks>
[ExcludeFromCodeCoverage]
internal static class SuppressionJustification {

    /// <summary>Validate arguments of public methods.</summary>
    internal static class CA1062 {

        /// <summary>
        /// The parameter comes from ASP.NET Core model binding or from xUnit's theory data, so no
        /// caller exists that could pass null and the guard would be unreachable — and unreachable
        /// code cannot be covered.
        /// </summary>
        /// <remarks>
        /// Per-site rather than an .editorconfig section for <c>tests/**</c>, which would also
        /// silence the rule on a future test helper that really is called by hand.
        /// </remarks>
        internal const string ArgumentSuppliedByTheFramework =
            "The parameter comes from ASP.NET Core model binding or from xUnit's own theory data, "
          + "so no caller exists that could pass null and no guard could ever run.";

    }

    /// <summary>Identifiers should differ by more than case.</summary>
    internal static class CA1708 {

        /// <summary>
        /// The rule is right, and the fixture exists because the shape it warns about is a shape a
        /// consumer's enum can have — one this library resolved wrongly. Renaming the member to
        /// satisfy the rule would delete the test.
        /// </summary>
        /// <remarks>
        /// Per-site rather than a section for <c>tests/**</c> in .editorconfig: everywhere else in
        /// this suite the rule is one worth hearing, and these are the three fixtures that mean it.
        /// </remarks>
        internal const string TheShapeUnderTest =
            "Two members differing only by case is the shape under test — a consumer's enum can "
          + "declare it, and this library used to resolve it differently from System.Text.Json. "
          + "Renaming either member would remove what the fixture exists to exercise.";

    }

}
