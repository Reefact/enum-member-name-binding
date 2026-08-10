using System.Reflection;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// What a scan yields out of an assembly, and what it does with one that cannot hand over every type
/// it declares.
/// </summary>
/// <remarks>
/// The assemblies here are stand-ins rather than real ones, for the reason written at the top of
/// <see cref="DiscoveryTests" />: registration installs a converter process-wide for every contract
/// enum the scan finds, so scanning a real assembly full of fixtures would reach well beyond the test
/// that asked for it. A stand-in hands over exactly the types the test names, and nothing else.
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
