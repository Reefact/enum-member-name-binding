using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// A malformed contract must fail loudly at start-up, never silently at request time.
/// </summary>
public sealed class ContractValidationTests {

    public enum DuplicateNames {

        [JsonStringEnumMemberName("same")] First,
        [JsonStringEnumMemberName("same")] Second

    }

    public enum PaddedName {

        [JsonStringEnumMemberName(" padded ")] Padded

    }

    [Flags]
    public enum CommaInFlagsName {

        [JsonStringEnumMemberName("read,write")] ReadWrite = 1

    }

    public enum NumericAlias {

        [JsonStringEnumMemberName("first")]  First  = 1,
        [JsonStringEnumMemberName("uno")]    Uno    = 1,
        [JsonStringEnumMemberName("second")] Second = 2

    }

    [Fact]
    public void two_members_cannot_declare_the_same_public_name() {
        EnumContractException exception = Assert.Throws<EnumContractException>(() => EnumContract.For(typeof(DuplicateNames)));

        Assert.Equal(typeof(DuplicateNames), exception.EnumType);
        Assert.Contains(exception.Problems, p => p.Contains("'same'", StringComparison.Ordinal));
    }

    [Fact]
    public void a_public_name_cannot_have_surrounding_whitespace() {
        EnumContractException exception = Assert.Throws<EnumContractException>(() => EnumContract.For(typeof(PaddedName)));

        Assert.Contains(exception.Problems, p => p.Contains("whitespace", StringComparison.Ordinal));
    }

    [Fact]
    public void a_flags_public_name_cannot_contain_a_comma() {
        EnumContractException exception = Assert.Throws<EnumContractException>(() => EnumContract.For(typeof(CommaInFlagsName)));

        Assert.Contains(exception.Problems, p => p.Contains("comma", StringComparison.Ordinal));
    }

    [Fact]
    public void the_error_message_names_the_type_and_every_problem() {
        EnumContractException exception = Assert.Throws<EnumContractException>(() => EnumContract.For(typeof(DuplicateNames)));

        Assert.Contains(typeof(DuplicateNames).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains("Second", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void distinct_names_sharing_one_numeric_value_are_both_accepted() {
        EnumContract contract = EnumContract.For(typeof(NumericAlias));

        Assert.True(contract.TryParse("first", out object first));
        Assert.True(contract.TryParse("uno", out object uno));
        Assert.Equal(first, uno);
    }

    [Fact]
    public void a_plain_enum_is_not_a_contract() {
        Assert.False(EnumContract.For(typeof(PlainPriority)).IsContract);
        Assert.True(EnumContract.For(typeof(ProductStatus)).IsContract);
    }

    [Fact]
    public void the_allowed_values_are_listed_in_declaration_order() {
        Assert.Equal("available, out_of_stock, discontinued", EnumContract.For(typeof(ProductStatus)).AllowedValues);
    }

    /// <summary>
    /// A type that is not an enum is refused, and the refusal names the argument the caller actually
    /// supplied.
    /// </summary>
    /// <remarks>
    /// <c>EnumTypes</c> is the escape hatch for a caller holding a <see cref="Type" /> at run time,
    /// so it is the one way a non-enum can get this far — <c>AddEnum&lt;TEnum&gt;()</c> states the
    /// constraint in the type system.
    ///
    /// <c>ParamName</c> is asserted because it is the part a caller can act on, and the part nothing
    /// else here would notice. It must stay <c>options</c>, the name of the lambda parameter of
    /// <c>AddEnumMemberNameBinding(options =&gt; ...)</c>; the name of whatever local the
    /// implementation unpacks that list into would mean nothing to the person reading the exception.
    /// </remarks>
    [Fact]
    public void a_type_that_is_not_an_enum_is_refused_against_the_caller_s_own_argument() {
        EnumMemberNameBindingOptions options = new();
        options.EnumTypes.Add(typeof(string));

        ArgumentException exception = Assert.Throws<ArgumentException>(() => EnumMemberNameBindingRegistry.Register(options));

        Assert.Equal("options", exception.ParamName);
        Assert.Contains("is not an enum", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void a_partial_contract_is_rejected_by_default() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<PartiallyAnnotated>();

        EnumContractException exception = Assert.Throws<EnumContractException>(() => EnumMemberNameBindingRegistry.Register(options));

        Assert.Equal(typeof(PartiallyAnnotated), exception.EnumType);
        Assert.Contains(exception.Problems, p => p.Contains("'Two'", StringComparison.Ordinal));
        Assert.Contains("public contract", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EnumMemberNameBindingOptions.AllowPartialContracts), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void a_partial_contract_is_accepted_when_explicitly_allowed() {
        EnumMemberNameBindingOptions options = new() { AllowPartialContracts = true };
        options.AddEnum<PartiallyAnnotated>();

        IReadOnlyList<Type> registered = EnumMemberNameBindingRegistry.Register(options);

        Assert.Contains(typeof(PartiallyAnnotated), registered);
    }

    [Fact]
    public void a_fully_annotated_contract_is_never_partial() {
        Assert.Empty(EnumContract.For(typeof(ProductStatus)).UnannotatedMembers);
        // AsEnumerable, because ImmutableArray<T>.Equals compares the underlying array by reference.
        Assert.Equal(["Two"], EnumContract.For(typeof(PartiallyAnnotated)).UnannotatedMembers.AsEnumerable());
    }

}
