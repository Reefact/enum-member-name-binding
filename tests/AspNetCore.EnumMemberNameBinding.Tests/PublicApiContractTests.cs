using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.Tests;

public enum Untouched {

    Low,
    High

}

/// <summary>
/// What the public API hands out, and what it refuses to take.
/// </summary>
public sealed class PublicApiContractTests {

    public enum Contractual {

        [JsonStringEnumMemberName("first")]  First,
        [JsonStringEnumMemberName("second")] Second

    }

    /// <summary>
    /// The returned list is cached and reused, including by the OpenAPI package, so a caller must not
    /// be able to write through it. Today a collection expression targeting <c>IReadOnlyList</c>
    /// happens to synthesize a read-only wrapper rather than an array, but that is a compiler
    /// implementation detail — the guarantee is stated in the type instead.
    /// </summary>
    [Fact]
    public void the_public_names_cannot_be_written_through() {
        IReadOnlyList<string> names = EnumMemberNames.GetPublicNames(typeof(Contractual))!;

        Assert.IsType<ImmutableArray<string>>(names);
        Assert.Throws<InvalidCastException>(() => (string[])names);

        // ImmutableArray does implement IList for compatibility, but every mutation refuses.
        Assert.Throws<NotSupportedException>(() => ((IList<string>)names)[0] = "corrupted");
        Assert.Equal("first", EnumMemberNames.GetPublicNames(typeof(Contractual))![0]);
    }

    [Fact]
    public void handing_out_the_names_twice_yields_the_same_content() {
        IReadOnlyList<string> first = EnumMemberNames.GetPublicNames(typeof(Contractual))!;
        IReadOnlyList<string> second = EnumMemberNames.GetPublicNames(typeof(Contractual))!;

        Assert.Equal(["first", "second"], first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// Registering an enum that declares nothing would change how an ordinary enum binds and
    /// serializes — the one thing this library promises never to do. Naming one explicitly is a
    /// mistake, so it is reported rather than honoured.
    /// </summary>
    [Fact]
    public void registering_an_enum_without_a_contract_is_refused() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<Untouched>();

        EnumContractException exception = Assert.Throws<EnumContractException>(() => EnumMemberNameBindingRegistry.Register(options));

        Assert.Equal(typeof(Untouched), exception.EnumType);
        Assert.Contains("no contract to apply", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>AllowPartialContracts</c> governs an incomplete contract, not the absence of one. It must
    /// not become a way to quietly adopt an ordinary enum.
    /// </summary>
    [Fact]
    public void allowing_partial_contracts_does_not_let_an_ordinary_enum_be_adopted() {
        EnumMemberNameBindingOptions options = new() { AllowPartialContracts = true };
        options.AddEnum<Untouched>();

        Assert.Throws<EnumContractException>(() => EnumMemberNameBindingRegistry.Register(options));
    }

    /// <summary>
    /// The distinction the refusal rests on: an enum with no attribute at all declares no contract,
    /// so the scan passes it by rather than adopting it. Only naming it explicitly is an error.
    /// </summary>
    [Fact]
    public void an_enum_without_a_contract_is_not_a_contract() {
        Assert.False(EnumContract.For(typeof(Untouched)).IsContract);
        Assert.True(EnumContract.For(typeof(Contractual)).IsContract);
        Assert.Null(EnumMemberNames.GetPublicNames(typeof(Untouched)));
    }

}
