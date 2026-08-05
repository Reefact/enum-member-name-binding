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
    public IList<Assembly> Assemblies { get; } = [];

    /// <summary>
    /// Enum types registered explicitly, whether or not they carry the attribute.
    /// </summary>
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
