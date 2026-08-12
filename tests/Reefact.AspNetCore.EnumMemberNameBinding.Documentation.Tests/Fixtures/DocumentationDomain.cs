using System.Text.Json.Serialization;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Documentation.Tests.Fixtures;

/// <summary>
/// The illustrative domain a sample may name without declaring it.
/// </summary>
/// <remarks>
/// <para>
/// A page tells one story across several fenced blocks: the first declares <c>ProductStatus</c>, a
/// later one binds it from a query string. Each block is compiled on its own — a rule page shows
/// the same enum twice, wrong then right, and those two cannot share a compilation — so a block
/// that names an enum declared three blocks earlier would fail on a symbol the reader can see.
/// This is that symbol, provided once for every sample.
/// </para>
/// <para>
/// It is imported by a <c>using</c> rather than declared beside the sample, which is what lets a
/// page declare its own <c>ProductStatus</c> and win: a type declared in the sample's own namespace
/// beats one reached through a <c>using</c>, so the two never collide.
/// </para>
/// <para>
/// The contract carried here is deliberately valid. It lives in a referenced assembly rather than
/// in the sample's syntax tree, so the analyzers reporting on a sample never see it — but a fixture
/// that violated a rule this repository ships would still be a poor thing to have written down.
/// </para>
/// </remarks>
public enum ProductStatus {

    [JsonStringEnumMemberName("available")]    Available,
    [JsonStringEnumMemberName("out_of_stock")] OutOfStock,
    [JsonStringEnumMemberName("discontinued")] Discontinued

}
