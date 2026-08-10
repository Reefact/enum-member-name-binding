namespace AspNetCore.EnumMemberNameBinding.OpenApi;

/// <summary>
/// The explanation carried by the trimming annotations of this package.
/// </summary>
/// <remarks>
/// Duplicated from the main package rather than shared through a public constant: the wording is an
/// implementation detail of the annotations and has no business in a stable API. Only the trimming
/// constraint applies here — the transformer reads an enum's public names and nothing more, so it
/// never reaches the code that builds a converter at run time.
/// </remarks>
internal static class TrimmingMessages {

    internal const string Reflection = "Enum member name binding reads enum metadata reflectively and is not compatible with trimming.";

}
