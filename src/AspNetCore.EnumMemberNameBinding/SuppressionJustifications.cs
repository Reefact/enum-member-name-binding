using System.Diagnostics.CodeAnalysis;

namespace AspNetCore.EnumMemberNameBinding;

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
/// </remarks>
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
        internal const string RequirementCarriedByConstructor =
            "The constructor carries the requirement; an instance cannot exist without it.";

    }

    /// <summary>Code that needs runtime code generation.</summary>
    internal static class IL3050 {

        /// <summary>
        /// The same reason as <see cref="IL2026.RequirementCarriedByConstructor" />, for the same
        /// member: it carries both constraints, and the constructor answers for both.
        /// </summary>
        /// <remarks>
        /// Deliberately an alias rather than a second copy of the sentence. One member is excused
        /// once, for one reason, under two rules — so rewording it must reword both, and an alias
        /// makes that true by construction instead of by whoever remembers.
        /// <para>
        /// The constraints themselves are not the same and are annotated separately elsewhere:
        /// reading enum metadata needs reflection but no code generation, so only the path that
        /// really builds a converter at run time is told about dynamic code. See
        /// <see cref="TrimmingMessages" />.
        /// </para>
        /// </remarks>
        internal const string RequirementCarriedByConstructor = IL2026.RequirementCarriedByConstructor;

    }

}
