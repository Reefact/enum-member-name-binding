using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.OpenApi;

namespace AspNetCore.EnumMemberNameBinding.OpenApi;

/// <summary>
/// The reasons carried by this package's analyzer suppressions, addressed by the rule they excuse.
/// </summary>
/// <remarks>
/// Shaped after the rule catalogues the suppressions already reference, so a use site reads the same
/// way on both sides: <c>TrimRule.IL2026.Id</c> names the rule, and
/// <c>SuppressionJustification.IL2026</c> names why it was silenced here.
///
/// Duplicated from the main package rather than shared, for the same reason its
/// <see cref="TrimmingMessages" /> is: the wording is an implementation detail of the annotations
/// and has no business in a stable API. Only the trimming constraint applies in this package — the
/// transformer reads an enum's public names and nothing more, so it never reaches the code that
/// builds a converter at run time, and there is no IL3050 to excuse.
///
/// Excluded from coverage for the reason given on the main package's copy: a class of constants has
/// no behaviour to measure, and a <c>const</c> initialised from another constant is nevertheless
/// counted as an executable line by Sonar's C# sensor — uncoverable by construction.
/// </remarks>
[ExcludeFromCodeCoverage]
internal static class SuppressionJustification {

    /// <summary>Reflection the trimmer cannot follow.</summary>
    internal static class IL2026 {

        /// <summary>
        /// The transformer's entry point reaches trimming-unsafe code without annotating itself,
        /// because this type's constructor already carries
        /// <see cref="RequiresUnreferencedCodeAttribute" />.
        /// </summary>
        /// <remarks>
        /// The rule is right in general: a member that reaches reflection the trimmer cannot follow
        /// must say so, or a trimmed application loses the metadata and fails at run time. It does
        /// not apply here because the warning has already been delivered at the only door — no
        /// instance can exist without the annotated constructor — and this member implements
        /// <see cref="IOpenApiSchemaTransformer" />, so its signature is the interface's and cannot
        /// carry the annotation itself.
        /// <para>
        /// This one is <c>Unconditional</c>, which means it survives into the IL and is read by the
        /// linker long after the compiler is gone. A wrong one is not a warning that stays quiet: it
        /// is a <c>TypeLoadException</c> in a published build, on a path nobody exercised before
        /// publishing.
        /// </para>
        /// <para>
        /// What would make it wrong: a second way to obtain an instance that does not pass through
        /// the annotated constructor. Today there is one call site, in
        /// <c>AddEnumMemberNames()</c>, and the type is internal — both of which are what keep the
        /// claim checkable.
        /// </para>
        /// </remarks>
        internal const string RequirementCarriedByConstructor =
            "The constructor carries the requirement; an instance cannot exist without it.";

    }

}
