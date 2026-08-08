namespace AspNetCore.EnumMemberNameBinding.Analyzers.Tests;

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
/// </remarks>
internal static class SuppressionJustification {

    /// <summary>Simplify collection initialization.</summary>
    internal static class IDE0028 {

        /// <summary>
        /// A <c>TheoryData</c> is built with an object initializer rather than the collection
        /// expression this rule asks for, because the two analyzers disagree and one of them fails
        /// the build.
        /// </summary>
        /// <remarks>
        /// The rule is right in general — a collection expression is the modern form and reads
        /// better. Written that way here, CA1825 ("avoid zero-length array allocations") fires on
        /// the 10.0.100 analyzers, and <c>TreatWarningsAsErrors</c> in Directory.Build.props turns
        /// that into a failed build. 10.0.100 is not incidental: it is the floor declared in
        /// global.json and one of the two SDKs the CI matrix pins, precisely so a disagreement
        /// between analyzer versions is caught rather than discovered by a consumer.
        /// <para>
        /// So obeying this rule would turn a CI leg red, and the object initializer is what keeps it
        /// green. Note the conflict does not reproduce on a newer SDK — a local build on 10.0.110
        /// accepts the collection expression — which is exactly why the decision is written down
        /// rather than rediscovered by whoever next has a newer SDK than CI.
        /// </para>
        /// <para>
        /// What would make it obsolete: the floor in global.json moving past the analyzers that
        /// disagree. On that day this suppression should be removed and the collection expression
        /// written, not kept out of habit.
        /// </para>
        /// </remarks>
        internal const string CollectionExpressionBreaksTheFloorSdk =
            "The collection expression this rule asks for trips CA1825 on the 10.0.100 analyzers — "
          + "the SDK floor in global.json, and one of the two CI legs, where a warning is an error. "
          + "The object initializer is what keeps that leg green.";

    }

}
