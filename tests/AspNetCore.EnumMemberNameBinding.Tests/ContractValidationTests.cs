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

    public enum EmptyName {

        [JsonStringEnumMemberName("")] Nameless

    }

    public enum PaddedName {

        [JsonStringEnumMemberName(" padded ")] Padded

    }

    [Flags]
    public enum CommaInFlagsName {

        [JsonStringEnumMemberName("read,write")] ReadWrite = 1

    }

    public enum CommaInOrdinaryName {

        [JsonStringEnumMemberName("read,write")] ReadWrite = 1

    }

    public enum NumericAlias {

        [JsonStringEnumMemberName("first")]  First  = 1,
        [JsonStringEnumMemberName("uno")]    Uno    = 1,
        [JsonStringEnumMemberName("second")] Second = 2

    }

    /// <summary>Two members left unannotated, where <c>PartiallyAnnotated</c> leaves one.</summary>
    public enum PartiallyAnnotatedTwice {

        [JsonStringEnumMemberName("one")] One,
        Two,
        Three

    }

    [Fact]
    public void two_members_cannot_declare_the_same_public_name() {
        EnumContractException exception = Check.ThatCode(() => EnumContract.For(typeof(DuplicateNames))).Throws<EnumContractException>().Value;

        Check.That(exception.EnumType).IsEqualTo(typeof(DuplicateNames));
        Check.That(exception.Problems).HasElementThatMatches(p => p.Contains("'same'", StringComparison.Ordinal));
    }

    /// <summary>
    /// The first of the malformed-name tests, and the reason they run in the order they do: every one
    /// after it inspects a character of the name, which an empty one does not have.
    /// </summary>
    [Fact]
    public void a_public_name_cannot_be_empty() {
        EnumContractException exception = Check.ThatCode(() => EnumContract.For(typeof(EmptyName))).Throws<EnumContractException>().Value;

        Check.That(exception.Problems).HasElementThatMatches(p => p.Contains("empty name", StringComparison.Ordinal));
        Check.That(exception.Problems).HasElementThatMatches(p => p.Contains(nameof(EmptyName.Nameless), StringComparison.Ordinal));
    }

    [Fact]
    public void a_public_name_cannot_have_surrounding_whitespace() {
        EnumContractException exception = Check.ThatCode(() => EnumContract.For(typeof(PaddedName))).Throws<EnumContractException>().Value;

        Check.That(exception.Problems).HasElementThatMatches(p => p.Contains("whitespace", StringComparison.Ordinal));
    }

    /// <summary>
    /// Refused on a <c>[Flags]</c> enum, exactly where <c>System.Text.Json</c> refuses it — its own
    /// message says "Flags enums must <em>additionally</em> not contain commas".
    /// </summary>
    [Fact]
    public void a_public_name_on_a_flags_enum_cannot_contain_a_comma() {
        EnumContractException exception = Check.ThatCode(() => EnumContract.For(typeof(CommaInFlagsName))).Throws<EnumContractException>().Value;

        Check.That(exception.Problems).HasElementThatMatches(p => p.Contains("comma", StringComparison.Ordinal));
    }

    /// <summary>
    /// And accepted anywhere else, because the serializer accepts it — the pair is the point.
    /// </summary>
    /// <remarks>
    /// The refusal used to cover both, on the reading that a comma separates values everywhere so a
    /// name carrying one can never be told apart from a combination. It can: the serializer looks the
    /// whole value up as a name before splitting, and only reaches the split when no name spells it.
    /// Refusing the shape here made a registered enum stricter than the same enum left alone —
    /// see <c>ReadParityTests</c> and <c>FormattingParityTests</c>, which hold the round trip against
    /// the serializer rather than against this expectation.
    /// </remarks>
    [Fact]
    public void a_public_name_on_an_ordinary_enum_may_contain_a_comma() {
        EnumContract contract = EnumContract.For(typeof(CommaInOrdinaryName));

        Check.That(contract.PublicNames.ToArray()).ContainsExactly("read,write");
    }

    [Fact]
    public void the_error_message_names_the_type_and_every_problem() {
        EnumContractException exception = Check.ThatCode(() => EnumContract.For(typeof(DuplicateNames))).Throws<EnumContractException>().Value;

        Check.That(exception.Message).Contains(typeof(DuplicateNames).FullName!);
        Check.That(exception.Message).Contains("Second");
    }

    [Fact]
    public void distinct_names_sharing_one_numeric_value_are_both_accepted() {
        EnumContract contract = EnumContract.For(typeof(NumericAlias));

        Check.That(contract.TryParse("first", out object? first)).IsTrue();
        Check.That(contract.TryParse("uno", out object? uno)).IsTrue();
        Check.That(uno).IsEqualTo(first);
    }

    [Fact]
    public void a_plain_enum_is_not_a_contract() {
        Check.That(EnumContract.For(typeof(PlainPriority)).IsContract).IsFalse();
        Check.That(EnumContract.For(typeof(ProductStatus)).IsContract).IsTrue();
    }

    /// <summary>
    /// Declaration order, and not the order <c>Enum.GetNames</c> returns: it is what the OpenAPI
    /// document lists and what a reader of the enum sees, so the two have to be the same walk.
    /// </summary>
    [Fact]
    public void the_public_names_are_listed_in_declaration_order() {
        IEnumerable<string> names = EnumContract.For(typeof(ProductStatus)).PublicNames;

        Check.That(names).ContainsExactly("available", "out_of_stock", "discontinued");
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

        ArgumentException exception = Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options)).Throws<ArgumentException>().Value;

        Check.That(exception.ParamName).IsEqualTo("options");
        Check.That(exception.Message).Contains("is not an enum");
    }

    [Fact]
    public void a_partial_contract_is_rejected_by_default() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<PartiallyAnnotated>();

        EnumContractException exception = Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options)).Throws<EnumContractException>().Value;

        Check.That(exception.EnumType).IsEqualTo(typeof(PartiallyAnnotated));
        Check.That(exception.Problems).HasElementThatMatches(p => p.Contains("'Two'", StringComparison.Ordinal));
        Check.That(exception.Message).Contains("public contract");
        Check.That(exception.Message).Contains(nameof(EnumMemberNameBindingOptions.AllowPartialContracts));
    }

    /// <summary>
    /// The refusal names every member left unannotated, and its verb agrees with how many there are.
    /// Both counts are asserted together because a message that agrees for one and not for the other
    /// is the only way this can be wrong.
    /// </summary>
    [Fact]
    public void the_refusal_of_a_partial_contract_agrees_in_number() {
        EnumMemberNameBindingOptions one = new();
        one.AddEnum<PartiallyAnnotated>();
        EnumMemberNameBindingOptions several = new();
        several.AddEnum<PartiallyAnnotatedTwice>();

        EnumContractException single = Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(one)).Throws<EnumContractException>().Value;
        EnumContractException plural = Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(several)).Throws<EnumContractException>().Value;

        Check.That(single.Message).Contains("'Two' carries no");
        Check.That(plural.Message).Contains("'Two', 'Three' carry no");
    }

    /// <summary>
    /// The guard behind the refusal above: a caller holding a <see cref="Type" /> can hand over
    /// anything, and the contract of a type that is not an enum cannot be resolved at all.
    /// </summary>
    [Fact]
    public void the_contract_of_a_type_that_is_not_an_enum_cannot_be_resolved() {
        ArgumentException exception = Check.ThatCode(() => EnumContract.For(typeof(string))).Throws<ArgumentException>().Value;

        Check.That(exception.ParamName).IsEqualTo("enumType");
        Check.That(exception.Message).Contains("is not an enum");
    }

    [Fact]
    public void a_partial_contract_is_accepted_when_explicitly_allowed() {
        EnumMemberNameBindingOptions options = new() { AllowPartialContracts = true };
        options.AddEnum<PartiallyAnnotated>();

        IReadOnlyList<Type> registered = EnumMemberNameBindingRegistry.Register(options);

        Check.That(registered).Contains(typeof(PartiallyAnnotated));
    }

    [Fact]
    public void a_fully_annotated_contract_is_never_partial() {
        Check.That(EnumContract.For(typeof(ProductStatus)).UnannotatedMembers).IsEmpty();
        // AsEnumerable, because ImmutableArray<T>.Equals compares the underlying array by reference.
        Check.That(EnumContract.For(typeof(PartiallyAnnotated)).UnannotatedMembers.AsEnumerable()).ContainsExactly("Two");
    }

}
