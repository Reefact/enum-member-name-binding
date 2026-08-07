using System.Reflection;

namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// Controls which enums take part in enum member name binding.
/// </summary>
public sealed class EnumMemberNameBindingOptions {

    /// <summary>
    /// Assemblies scanned for enums carrying <c>[JsonStringEnumMemberName]</c>.
    /// When neither this nor <see cref="EnumTypes" /> is populated, the entry assembly is scanned.
    /// </summary>
    /// <remarks>
    /// The supported way to fill this is <see cref="ScanAssemblyContaining{T}" />. The list itself is
    /// the escape hatch for a caller holding an <see cref="Assembly" /> at run time, and it stays a
    /// mutable <see cref="IList{T}" /> for the same reason <c>MvcOptions.Conventions</c> and
    /// <c>JsonSerializerOptions.Converters</c> do. The consequence is that nothing validates an entry
    /// at the moment it is added; a bad one is reported at start-up instead.
    /// </remarks>
    public IList<Assembly> Assemblies { get; } = [];

    /// <summary>
    /// Enum types registered explicitly, bypassing the assembly scan.
    /// </summary>
    /// <remarks>
    /// Each must declare a contract — at least one member carrying <c>[JsonStringEnumMemberName]</c>.
    /// An enum that declares none is refused rather than adopted: taking it over would change how an
    /// ordinary enum binds and serializes, and <see cref="AllowPartialContracts" /> does not make that
    /// acceptable. It governs an incomplete contract, not the absence of one.
    ///
    /// The supported way to fill this is <see cref="AddEnum{TEnum}" />, which states the constraint in
    /// the type system. The list is the escape hatch for a caller holding a <see cref="Type" /> at run
    /// time, and like <see cref="Assemblies" /> it validates nothing at the point of the addition.
    /// </remarks>
    public IList<Type> EnumTypes { get; } = [];

    /// <summary>
    /// Whether an enum may declare a public name on some members only. Defaults to
    /// <see langword="false" />: a partial contract is rejected at start-up.
    /// </summary>
    /// <remarks>
    /// A member without <c>[JsonStringEnumMemberName]</c> keeps its C# name, and that name becomes
    /// part of the public contract — which is precisely what declaring a contract is meant to avoid.
    /// Forgetting one member is a mistake, not a choice, so it fails loudly.
    /// <para>
    /// Set to <see langword="true" /> for an enum you do not own and cannot annotate. The runtime
    /// behaviour then matches <c>System.Text.Json</c> exactly: the unannotated members answer to
    /// their C# name, case-insensitively.
    /// </para>
    /// </remarks>
    public bool AllowPartialContracts { get; set; }

    /// <summary>
    /// Whether the MVC <c>System.Text.Json</c> options are configured to serialize the registered
    /// enums as strings. Defaults to <see langword="true" />.
    /// </summary>
    /// <remarks>
    /// A converter is registered per enum type; the global <c>JsonStringEnumConverter</c> factory is
    /// never installed, so enums that declare no contract keep their existing wire format.
    /// </remarks>
    public bool ConfigureJsonSerialization { get; set; } = true;

    /// <summary>Scans the assembly containing <typeparamref name="T" />.</summary>
    public EnumMemberNameBindingOptions ScanAssemblyContaining<T>() {
        Assemblies.Add(typeof(T).Assembly);

        return this;
    }

    /// <summary>Registers a single enum type.</summary>
    public EnumMemberNameBindingOptions AddEnum<TEnum>() where TEnum : struct, Enum {
        EnumTypes.Add(typeof(TEnum));

        return this;
    }

}
