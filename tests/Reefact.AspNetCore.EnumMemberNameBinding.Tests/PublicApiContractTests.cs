using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Tests;

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

    [Flags]
    public enum Combinable {

        [JsonStringEnumMemberName("read")]  Read  = 1,
        [JsonStringEnumMemberName("write")] Write = 2

    }

    /// <summary>[Flags] without a contract: the attribute alone declares nothing.</summary>
    [Flags]
    public enum CombinableWithoutAContract {

        None = 0,
        One  = 1

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

        Check.That(names).IsInstanceOf<ImmutableArray<string>>();
        Check.ThatCode(() => (string[])names).Throws<InvalidCastException>();

        // ImmutableArray does implement IList for compatibility, but every mutation refuses.
        Check.ThatCode(() => ((IList<string>)names)[0] = "corrupted").Throws<NotSupportedException>();
        Check.That(EnumMemberNames.GetPublicNames(typeof(Contractual))![0]).IsEqualTo("first");
    }

    [Fact]
    public void handing_out_the_names_twice_yields_the_same_content() {
        IReadOnlyList<string> first = EnumMemberNames.GetPublicNames(typeof(Contractual))!;
        IReadOnlyList<string> second = EnumMemberNames.GetPublicNames(typeof(Contractual))!;

        Check.That(first).ContainsExactly("first", "second");
        Check.That(second).IsEqualTo(first);
    }

    /// <summary>
    /// A nullable enum is unwrapped rather than refused. It is the shape an optional action parameter
    /// arrives in, and the shape a document generator hands over for one.
    /// </summary>
    [Fact]
    public void a_nullable_enum_answers_for_the_enum_it_wraps() {
        Check.That(EnumMemberNames.GetPublicNames(typeof(Contractual?))!).ContainsExactly("first", "second");
        Check.That(EnumMemberNames.IsFlagsContract(typeof(Combinable?))).IsTrue();
    }

    /// <summary>
    /// A type that is not an enum is answered rather than refused. Both methods are called on every
    /// type a document generator walks past, and most of them are not enums.
    /// </summary>
    [Fact]
    public void a_type_that_is_not_an_enum_is_answered_with_nothing() {
        Check.That(EnumMemberNames.GetPublicNames(typeof(string))).IsNull();
        Check.That(EnumMemberNames.IsFlagsContract(typeof(string))).IsFalse();
    }

    /// <summary>
    /// Both halves of "flags contract" are load-bearing, so both are asserted alone: an enum
    /// carrying [Flags] and no contract is not one this package describes, and a contract without
    /// [Flags] is not one whose values are an open set.
    /// </summary>
    /// <remarks>
    /// The second half read "accepts no combination", which is the one thing <c>[Flags]</c> does not
    /// decide. A comma-separated list is accepted on every enum — <see cref="EnumContract.TryParse" />
    /// has no <c>[Flags]</c> test on the parse path at all — so on a non-<c>[Flags]</c> contract
    /// <c>"first,second"</c> resolves to <c>0 | 1</c> exactly as it does with
    /// <c>System.Text.Json</c>, which splits before it looks at the attribute. What <c>[Flags]</c>
    /// decides is whether the values an application will bind are an open set rather than the
    /// declared members alone, which is what <see cref="EnumMemberNames.IsFlagsContract" /> says in
    /// its own documentation one file away.
    /// </remarks>
    [Fact]
    public void a_flags_contract_is_both_of_those_things_at_once() {
        Check.That(EnumMemberNames.IsFlagsContract(typeof(Combinable))).IsTrue();
        Check.That(EnumMemberNames.IsFlagsContract(typeof(Contractual))).IsFalse();
        Check.That(EnumMemberNames.IsFlagsContract(typeof(CombinableWithoutAContract))).IsFalse();
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

        EnumContractException exception = Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options)).Throws<EnumContractException>().Value;

        Check.That(exception.EnumType).IsEqualTo(typeof(Untouched));
        Check.That(exception.Message).Contains("no contract to apply");
    }

    /// <summary>
    /// <c>AllowPartialContracts</c> governs an incomplete contract, not the absence of one. It must
    /// not become a way to quietly adopt an ordinary enum.
    /// </summary>
    [Fact]
    public void allowing_partial_contracts_does_not_let_an_ordinary_enum_be_adopted() {
        EnumMemberNameBindingOptions options = new() { AllowPartialContracts = true };
        options.AddEnum<Untouched>();

        Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options)).Throws<EnumContractException>();
    }

    /// <summary>
    /// The distinction the refusal rests on: an enum with no attribute at all declares no contract,
    /// so the scan passes it by rather than adopting it. Only naming it explicitly is an error.
    /// </summary>
    [Fact]
    public void an_enum_without_a_contract_is_not_a_contract() {
        Check.That(EnumContract.For(typeof(Untouched)).IsContract).IsFalse();
        Check.That(EnumContract.For(typeof(Contractual)).IsContract).IsTrue();
        Check.That(EnumMemberNames.GetPublicNames(typeof(Untouched))).IsNull();
    }

}
