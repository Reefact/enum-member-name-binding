using System.Text.Json.Serialization;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// What registration resolves, before it installs anything: which enums are covered, in what order,
/// and how many times.
/// </summary>
/// <remarks>
/// None of these scan this test assembly, deliberately, and no longer for the reason this said.
/// Registration installed a converter process-wide once; it does not, and has not since the model
/// binder moved onto the host's own container — everything a call configures lives in the
/// container it was handed. What makes a scan here refuse is the assembly itself: it declares
/// contracts that are deliberately malformed, for <see cref="ContractValidationTests" />, and a
/// scan resolves every contract it meets. The assembly scanned instead is the library's own,
/// which declares no contract at all, so the branch is exercised on something that cannot refuse.
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
    /// <c>ScanAssemblyContaining&lt;T&gt;</c> is the supported way to fill <c>Assemblies</c>: it names
    /// the assembly declaring <c>T</c>, and hands the options back so several calls chain.
    /// </summary>
    /// <remarks>
    /// The type named is the library's own, for the reason at the top of this file — what it resolves
    /// to has to be an assembly this suite can afford to scan.
    /// </remarks>
    [Fact]
    public void scanning_the_assembly_containing_a_type_names_that_assembly() {
        EnumMemberNameBindingOptions options = new();

        EnumMemberNameBindingOptions returned = options.ScanAssemblyContaining<EnumContract>();

        Check.That(returned).IsSameReferenceAs(options);
        Check.That(options.Assemblies).ContainsExactly(typeof(EnumContract).Assembly);
        Check.That(EnumMemberNameBindingRegistry.Register(options)).IsEmpty();
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
    /// This proves it about the return value: nothing is reported as registered either, so a caller
    /// reading the result cannot conclude that the good entries went through. What it leaves behind
    /// in the container is <see cref="RegistrationRefusalTests" />'s subject, which also covers the
    /// refusal that comes from the platform rather than from here.
    /// </remarks>
    [Fact]
    public void a_refused_registration_reports_nothing_as_registered() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<NamedAlone>();
        options.AddEnum<NoContractAtAll>();

        Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options)).Throws<EnumContractException>();
    }

}
