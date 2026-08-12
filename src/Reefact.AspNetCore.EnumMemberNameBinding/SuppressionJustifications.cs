using System.Diagnostics.CodeAnalysis;

namespace Reefact.AspNetCore.EnumMemberNameBinding;

/// <summary>
/// The reasons carried by this package's analyzer suppressions, addressed by the rule they excuse.
/// </summary>
/// <remarks>
/// Shaped after the rule catalogues the suppressions already reference, so a use site reads the same
/// way on both sides: <c>TrimRule.IL2026.Id</c> names the rule, and
/// <c>SuppressionJustification.IL2026</c> names why it was silenced here.
///
/// Separate from <see cref="TrimmingMessages" /> although both concern trimming: those are written
/// for a consumer who meets the warning, these for a maintainer asking why one was answered this
/// way. Internal, and duplicated in the companion package rather than shared through a public
/// constant — a suppression's wording is an implementation detail and has no business in a stable
/// API, which this package's is now that 1.0.0 fixes it.
///
/// Excluded from coverage because there is no behaviour here to measure. That is true of any class
/// of constants, but this one made it visible: a constant initialised from another constant rather
/// than from a literal is counted as an executable line by Sonar's C# sensor. A <c>const</c>
/// compiles to no code at all, so no coverage report can ever mention it and no test can be written
/// to reach it — the gate would have failed forever on a line that does not exist at run time.
/// </remarks>
[ExcludeFromCodeCoverage]
internal static class SuppressionJustification {

    /// <summary>Reflection the trimmer cannot follow.</summary>
    internal static class IL2026 {

        /// <summary>
        /// An override reaches trimming-unsafe code without annotating itself, because the type's
        /// constructor already carries <see cref="RequiresUnreferencedCodeAttribute" />.
        /// </summary>
        /// <remarks>
        /// The rule is right in general: a member that reaches reflection the trimmer cannot follow
        /// must say so, or a trimmed application loses the metadata and fails at run time. It does
        /// not apply here because the warning has already been delivered at the only door. No
        /// instance of this type can exist without going through that constructor, so a caller has
        /// been told before ever reaching this member — and an override cannot carry the annotation
        /// itself anyway, since its signature is the base type's.
        /// <para>
        /// This one is <c>Unconditional</c>, which means it survives into the IL and is read by the
        /// linker long after the compiler is gone. A wrong one is not a warning that stays quiet: it
        /// is a <c>TypeLoadException</c> in a published build, on a path nobody exercised before
        /// publishing.
        /// </para>
        /// <para>
        /// What would make it wrong: a second way to obtain an instance that does not pass through
        /// the annotated constructor — a parameterless constructor, a deserializer, a static
        /// factory. If one is ever added, this suppression is the thing to revisit first.
        /// </para>
        /// </remarks>
        internal const string RequirementCarriedByConstructor = "The constructor carries the requirement; an instance cannot exist without it.";

    }

}
