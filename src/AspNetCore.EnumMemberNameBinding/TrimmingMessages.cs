namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// The explanations carried by the trimming and Native AOT annotations.
/// </summary>
/// <remarks>
/// Deliberately internal. The two constraints are distinct and are applied separately: reading an
/// enum's metadata needs reflection but no code generation, so a consumer should only be told about
/// dynamic code on a path that actually generates some.
/// </remarks>
internal static class TrimmingMessages {

    /// <summary>Reflection over enum metadata, or over the types of an assembly.</summary>
    internal const string Reflection =
        "Enum member name binding reads enum metadata reflectively and is not compatible with trimming.";

    /// <summary>Construction of a generic converter, or a System.Text.Json round trip, at run time.</summary>
    internal const string DynamicCode =
        "Enum member name binding builds converters for the enum type at run time and is not compatible with Native AOT.";

}
