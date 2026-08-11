using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// <c>EnumContractException</c> used to document itself as "raised at startup, never on a request",
/// and this package is the reason that was wrong: it resolves a contract while writing the document,
/// which under <c>MapOpenApi</c> is a request. An application using the companion on its own — a
/// configuration <c>openapi.md</c> supports — therefore starts fine and answers 500 on
/// <c>/openapi/v1.json</c>.
/// </summary>
/// <remarks>
/// What is pinned here is the link the companion owns: the call the transformer makes is the one that
/// raises, so nothing between it and the document swallows the failure. The 500 itself was measured by
/// hand rather than asserted, because an endpoint needs a compile-time type and a malformed enum
/// cannot be declared in this assembly — <c>EntryAssemblyScanTests</c> scans it, and the scan resolves
/// every contract it meets, so the refusal would land there instead and prove nothing.
/// <para>
/// Hence the emitted enum. It is a real type with real attributes, and it lives in a dynamic assembly,
/// which is exactly what keeps it out of the scan's way.
/// </para>
/// </remarks>
public sealed class ContractFailureReachesTheDocumentTests {

    [Fact]
    public void resolving_a_malformed_contract_raises_rather_than_describing_it_wrongly() {
        Type malformed = EmitEnumDeclaringOneNameTwice();

        EnumContractException exception = Check.ThatCode(() => EnumMemberNames.GetPublicNames(malformed))
                                               .Throws<EnumContractException>().Value;

        Check.That(exception.EnumType).IsEqualTo(malformed);
        Check.That(exception.Problems).HasOneElementOnly();
        Check.WithCustomMessage("the message has to name the collision, since it is all a reader of the 500 gets.")
             .That(exception.Problems.Single()).Contains("same");
    }

    /// <summary>The emitted enum is not the entry assembly's, which is the point of emitting it.</summary>
    [Fact]
    public void the_emitted_enum_is_out_of_reach_of_the_entry_assembly_scan() {
        Type malformed = EmitEnumDeclaringOneNameTwice();

        Check.That(malformed.IsEnum).IsTrue();
        Check.That(malformed.Assembly).IsNotEqualTo(Assembly.GetEntryAssembly());
    }

    /// <summary>An enum whose two members declare the same public name — <c>EMN0001</c> at build time.</summary>
    private static Type EmitEnumDeclaringOneNameTwice() {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("EmittedContracts"), AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("EmittedContracts");
        EnumBuilder declaration = module.DefineEnum("DeclaresOneNameTwice", TypeAttributes.Public, typeof(int));

        ConstructorInfo attribute = typeof(JsonStringEnumMemberNameAttribute).GetConstructor([typeof(string)])!;

        foreach ((string member, int value) in new[] { ("First", 1), ("Second", 2) }) {
            declaration.DefineLiteral(member, value).SetCustomAttribute(new CustomAttributeBuilder(attribute, ["same"]));
        }

        return declaration.CreateType();
    }

}
