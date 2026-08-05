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
