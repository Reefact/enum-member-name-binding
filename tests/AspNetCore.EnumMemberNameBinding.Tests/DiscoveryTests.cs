using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// What registration resolves, before it installs anything: which enums are covered, in what order,
/// and how many times.
/// </summary>
/// <remarks>
/// None of these scan this test assembly, deliberately. Registration installs a converter
/// process-wide for every contract enum it finds, and this assembly is full of them — a test that
/// scanned it would silently register enums other tests require to be untouched, and would do it in
/// whatever order xUnit happened to choose. The assembly scanned here is the library's own, which
/// declares no contract at all, so the branch is exercised without the side effect.
/// </remarks>
public sealed class DiscoveryTests {

    public enum NamedTwice {

        [JsonStringEnumMemberName("once")] Once

    }

    public enum NamedAlone {

        [JsonStringEnumMemberName("alone")] Alone

    }

    public enum NoContractAtAll {

        Plain

    }

    /// <summary>
    /// Naming the same type twice registers it once. The list is public and takes whatever a caller
    /// puts in it, so a duplicate is a mistake worth absorbing rather than reporting.
    /// </summary>
    [Fact]
    public void a_type_named_twice_is_registered_once() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<NamedTwice>();
        options.AddEnum<NamedTwice>();

        IReadOnlyList<Type> registered = EnumMemberNameBindingRegistry.Register(options);

        Check.That(registered).ContainsExactly(typeof(NamedTwice));
    }

    /// <summary>
    /// Naming a type at all means "these, and nothing else": no assembly is scanned, not even the
    /// entry one.
    /// </summary>
    /// <remarks>
    /// The assertion is that the result is exactly one type. This test assembly is the entry
    /// assembly under the runner and declares many contract enums, so a scan that ran here would
    /// return a great many more — which is what makes the single-element result meaningful rather
    /// than incidental.
    /// </remarks>
    [Fact]
    public void naming_a_type_scans_no_assembly() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<NamedAlone>();

        IReadOnlyList<Type> registered = EnumMemberNameBindingRegistry.Register(options);

        Check.That(registered).ContainsExactly(typeof(NamedAlone));
    }

    /// <summary>
    /// Naming an assembly means "scan this one": an assembly that declares no contract yields
    /// nothing, and no fallback to the entry assembly happens behind it.
    /// </summary>
    [Fact]
    public void naming_an_assembly_scans_that_one_and_falls_back_to_nothing() {
        EnumMemberNameBindingOptions options = new();
        options.Assemblies.Add(typeof(EnumContract).Assembly);

        IReadOnlyList<Type> registered = EnumMemberNameBindingRegistry.Register(options);

        Check.That(registered).IsEmpty();
    }

    /// <summary>
    /// An enum that declares no contract cannot be adopted by naming it. Taking it over would change
    /// how an ordinary enum binds and serializes, which is the one thing this library promises not to
    /// do.
    /// </summary>
    [Fact]
    public void an_enum_declaring_no_contract_cannot_be_named_explicitly() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<NoContractAtAll>();

        EnumContractException exception = Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options)).Throws<EnumContractException>().Value;

        Check.That(exception.EnumType).IsEqualTo(typeof(NoContractAtAll));
        Check.That(exception.Message).Contains("no contract to apply");
    }

    /// <summary>
    /// The refusal happens before anything is installed, however far down the list the bad entry
    /// sits.
    /// </summary>
    /// <remarks>
    /// The companion of <c>ProcessWideStateTests.a_refused_registration_installs_nothing_at_all</c>,
    /// which proves the same thing about the process. This one proves it about the return value:
    /// nothing is reported as registered either, so a caller reading the result cannot conclude that
    /// the good entries went through.
    /// </remarks>
    [Fact]
    public void a_refused_registration_reports_nothing_as_registered() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<NamedAlone>();
        options.AddEnum<NoContractAtAll>();

        Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options)).Throws<EnumContractException>();
    }

}
