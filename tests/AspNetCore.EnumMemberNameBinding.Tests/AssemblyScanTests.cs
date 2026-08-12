using System.Reflection;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// What a scan yields out of an assembly, and what it does with one that cannot hand over every type
/// it declares.
/// </summary>
/// <remarks>
/// The assemblies here are stand-ins rather than real ones, for the reason written at the top of
/// <see cref="DiscoveryTests" />: a scan resolves every contract it meets, and this assembly
/// declares malformed ones on purpose, so scanning it would refuse before proving anything about
/// the scan. A stand-in hands over exactly the types the test names, and nothing else.
/// </remarks>
public sealed class AssemblyScanTests {

    public enum Found {

        [JsonStringEnumMemberName("found")] Only

    }

    public enum FoundBesideItsOwnName {

        [JsonStringEnumMemberName("beside")] Only

    }

    public enum DeclaresNothing {

        Plain

    }

    /// <summary>
    /// An enum nested in a generic type, which a scan meets as the open form <c>Box`1+Colour</c>.
    /// </summary>
    /// <remarks>
    /// Declared with no contract, deliberately: the crash this pins happens before the contract is
    /// looked at, so an enum nobody annotated and nobody wants registered was enough to stop an
    /// application booting.
    /// </remarks>
    public sealed class Box<T> {

        public enum Colour {

            Red

        }

    }

    /// <summary>The same shape carrying a contract, so the scan has something to yield from it.</summary>
    public sealed class Crate<T> {

        public enum State {

            [JsonStringEnumMemberName("packed")] Packed

        }

    }

    /// <summary>
    /// An enum nested in a generic type is passed by rather than resolved.
    /// </summary>
    /// <remarks>
    /// <c>Assembly.GetTypes()</c> hands such an enum over in its open form, where
    /// <c>Type.IsEnum</c> is true and <c>ContainsGenericParameters</c> is true as well. Reading a
    /// member off it is not something reflection allows: <c>FieldInfo.GetValue</c> answers
    /// <c>ArgumentException: Specified type is not supported</c>, thrown from
    /// <c>Enum.InternalBoxEnum</c>, before the contract is so much as looked at.
    /// <para>
    /// So an enum nobody annotated, nobody registered and nobody wanted stopped the application
    /// booting, with an exception naming neither the type nor this package. Measured on a host
    /// declaring <c>public class Box&lt;T&gt; { public enum Colour { Red } }</c> and calling
    /// <c>AddEnumMemberNameBinding()</c> with no options at all.
    /// </para>
    /// </remarks>
    [Fact]
    public void a_scan_passes_by_an_enum_nested_in_a_generic_type() {
        Type openForm = typeof(Box<>).GetNestedType(nameof(Box<int>.Colour))!;
        Check.WithCustomMessage("the fixture must hand over the open form, which is the shape a scan meets.")
             .That(openForm.ContainsGenericParameters).IsTrue();

        EnumMemberNameBindingOptions options = new();
        options.Assemblies.Add(new StandInAssembly(openForm, typeof(Found)));

        Check.That(EnumMemberNameBindingRegistry.Register(options)).ContainsExactly(typeof(Found));
    }

    /// <summary>
    /// And it is passed by whether or not it declares a contract, because nothing can read one off
    /// it — this is not the scan declining to register it, it is the scan unable to look.
    /// </summary>
    [Fact]
    public void a_contract_nested_in_a_generic_type_is_passed_by_too() {
        Type openForm = typeof(Crate<>).GetNestedType(nameof(Crate<int>.State))!;

        EnumMemberNameBindingOptions options = new();
        options.Assemblies.Add(new StandInAssembly(openForm, typeof(Found)));

        Check.That(EnumMemberNameBindingRegistry.Register(options)).ContainsExactly(typeof(Found));
    }

    /// <summary>
    /// And what it does not keep: a closed form is registrable because it carries no generic
    /// parameter, not because it is closed. <see cref="Box{T}.Colour" /> declares no contract, so
    /// naming <c>Box&lt;int&gt;.Colour</c> is refused for that reason — as it was before the guard,
    /// and as any bare enum is.
    /// </summary>
    /// <remarks>
    /// Here because three sentences said otherwise. The commit that added the guard offered
    /// <c>AddEnum&lt;Box&lt;int&gt;.Colour&gt;()</c> as the way to register the enum it had just made
    /// the scan skip, in both changelogs and in the registry's own remark — an escape hatch that
    /// throws, on the very declaration the paragraph above it gives. The test beside this one uses
    /// <see cref="Crate{T}.State" /> precisely because it is annotated, which is what should have
    /// said so.
    /// </remarks>
    [Fact]
    public void the_closed_form_of_an_enum_declaring_nothing_is_refused_as_any_other_is() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<Box<int>.Colour>();

        EnumContractException refusal = Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options))
                                             .Throws<EnumContractException>().Value;

        Check.That(refusal.Problems).HasElementThatMatches(p => p.Contains("no contract to apply", StringComparison.Ordinal));
    }

    /// <summary>
    /// What the caller keeps: the closed form carries no generic parameter, so naming it explicitly
    /// registers it exactly as any other contract enum. The scan cannot reach it — no assembly
    /// declares <c>Crate&lt;int&gt;.State</c> — which is why naming it is the way in.
    /// </summary>
    [Fact]
    public void the_closed_form_of_such_a_contract_can_still_be_named() {
        Check.That(typeof(Crate<int>.State).ContainsGenericParameters).IsFalse();

        EnumMemberNameBindingOptions options = new();
        options.AddEnum<Crate<int>.State>();

        Check.That(EnumMemberNameBindingRegistry.Register(options)).ContainsExactly(typeof(Crate<int>.State));
    }

    /// <summary>
    /// The scan keeps the contract enums and passes everything else by — the enum that declares no
    /// contract as much as the types that are not enums at all.
    /// </summary>
    [Fact]
    public void a_scan_yields_the_contract_enums_and_leaves_the_rest_alone() {
        EnumMemberNameBindingOptions options = new();
        options.Assemblies.Add(new StandInAssembly(typeof(DeclaresNothing), typeof(Found), typeof(AssemblyScanTests)));

        Check.That(EnumMemberNameBindingRegistry.Register(options)).ContainsExactly(typeof(Found));
    }

    /// <summary>
    /// A type both named explicitly and reached by the scan is registered once. Only a scan can
    /// produce this: the explicit list deduplicates itself, and the two phases share one set so that
    /// the second never repeats the first.
    /// </summary>
    [Fact]
    public void a_type_named_explicitly_is_not_yielded_again_by_the_scan() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<FoundBesideItsOwnName>();
        options.Assemblies.Add(new StandInAssembly(typeof(FoundBesideItsOwnName)));

        Check.That(EnumMemberNameBindingRegistry.Register(options)).ContainsExactly(typeof(FoundBesideItsOwnName));
    }

    /// <summary>
    /// An assembly that cannot load every type it declares still hands over the ones it could.
    /// </summary>
    /// <remarks>
    /// The shape of an assembly built against a dependency that was not deployed:
    /// <see cref="Assembly.GetTypes" /> throws, and carries on the exception the types that did load.
    /// Refusing to start over a type nobody asked about would be the wrong answer — the scan is
    /// looking for contract enums, and one that failed to load is not one of them.
    /// </remarks>
    [Fact]
    public void an_assembly_that_cannot_load_every_type_still_yields_the_ones_it_could() {
        EnumMemberNameBindingOptions options = new();
        options.Assemblies.Add(new PartlyLoadedAssembly(typeof(Found)));

        Check.That(EnumMemberNameBindingRegistry.Register(options)).ContainsExactly(typeof(Found));
    }

    private sealed class StandInAssembly(params Type[] types) : Assembly {

        public override Type[] GetTypes() {
            return types;
        }

    }

    /// <summary>
    /// Reports one type it could not load, alongside the ones it could — which is exactly the shape
    /// <see cref="ReflectionTypeLoadException" /> carries: a null in <c>Types</c> for each failure.
    /// </summary>
    private sealed class PartlyLoadedAssembly(params Type[] loaded) : Assembly {

        public override Type[] GetTypes() {
            Type?[] withOneMissing = [.. loaded, null];

            throw new ReflectionTypeLoadException(withOneMissing, [new FileNotFoundException()]);
        }

    }

}
