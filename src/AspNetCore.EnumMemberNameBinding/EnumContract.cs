using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// The name/value mapping of a single enum type, resolved once and cached.
/// </summary>
/// <remarks>
/// The matching rules are a deliberate port of the ones <c>System.Text.Json</c> applies to the
/// request body, so that every input channel of an API accepts exactly the same vocabulary:
/// <list type="bullet">
///   <item>a member annotated with <c>[JsonStringEnumMemberName]</c> matches its declared name, and only that name, case-sensitively;</item>
///   <item>a member without the attribute matches its C# name, case-insensitively;</item>
///   <item>a <c>[Flags]</c> enum additionally accepts a comma-separated list of the above.</item>
/// </list>
/// Numeric values are never accepted — the equivalent of <c>allowIntegerValues: false</c>.
/// </remarks>
internal sealed class EnumContract {

    private static readonly ConcurrentDictionary<Type, EnumContract> Cache = new();

    private readonly FrozenDictionary<string, object> _byContractName;
    private readonly FrozenDictionary<string, object> _byClrName;
    private readonly FrozenDictionary<object, string> _names;
    private readonly bool _isFlags;
    private readonly JsonSerializerOptions _writeOptions;

    private EnumContract(Type enumType) {
        EnumType = enumType;
        _isFlags = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);

        Dictionary<string, object> byContractName = new(StringComparer.Ordinal);
        Dictionary<string, object> byClrName      = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<object, string> names          = [];
        List<(object, string)>     ordered        = [];
        List<string>               problems       = [];
        List<string>               unannotated    = [];
        Dictionary<string, string> declaredBy     = new(StringComparer.Ordinal);

        foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static)) {
            object value = field.GetValue(null)!;
            JsonStringEnumMemberNameAttribute? attribute = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();

            if (attribute is null) {
                byClrName.TryAdd(field.Name, value);
                names.TryAdd(value, field.Name);
                ordered.Add((value, field.Name));
                unannotated.Add(field.Name);
                continue;
            }

            IsContract = true;
            string name = attribute.Name;

            if (string.IsNullOrEmpty(name)) {
                problems.Add($"member '{field.Name}' declares an empty name.");
                continue;
            }

            if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1])) {
                problems.Add($"member '{field.Name}' declares the name '{name}', which has leading or trailing whitespace.");
                continue;
            }

            if (_isFlags && name.Contains(',', StringComparison.Ordinal)) {
                problems.Add($"member '{field.Name}' declares the name '{name}', which contains a comma. " +
                             "A comma separates values in a [Flags] enum and cannot appear inside a name.");
                continue;
            }

            if (!byContractName.TryAdd(name, value)) {
                problems.Add($"member '{field.Name}' declares the name '{name}', which is already declared by another member. " +
                             "Two members cannot share the same public name.");
                continue;
            }

            names.TryAdd(value, name);
            ordered.Add((value, name));
            declaredBy[name] = field.Name;
        }

        // A declared name is matched before an unannotated member's C# name, and case-sensitively,
        // so the shadowed member ends up answering to every casing of its name except its own.
        // The comparison is case-insensitive because that is how the C# names are looked up.
        foreach (KeyValuePair<string, string> declared in declaredBy) {
            string? shadowed = unannotated.Find(member => string.Equals(member, declared.Key, StringComparison.OrdinalIgnoreCase));
            if (shadowed is null) { continue; }

            problems.Add($"member '{declared.Value}' declares the public name '{declared.Key}', which is also the C# name " +
                         $"of member '{shadowed}'. The value '{declared.Key}' resolves to '{declared.Value}', leaving " +
                         $"'{shadowed}' reachable only through a different casing. Rename the public name, or annotate " +
                         $"'{shadowed}' as well.");
        }

        if (problems.Count > 0) {
            throw new EnumContractException(enumType, problems);
        }

        _byContractName = byContractName.ToFrozenDictionary(StringComparer.Ordinal);
        _byClrName      = byClrName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _names          = names.ToFrozenDictionary();
        PublicNames         = [.. ordered.Select(static o => o.Item2)];
        UnannotatedMembers  = [.. unannotated];
        AllowedValues       = string.Join(", ", PublicNames);
        _writeOptions       = new JsonSerializerOptions {
            Converters = {
                (JsonConverter)Activator.CreateInstance(typeof(JsonStringEnumConverter<>).MakeGenericType(enumType), null, false)!
            }
        };
    }

    /// <summary>The described enum type.</summary>
    internal Type EnumType { get; }

    /// <summary>Whether at least one member carries <c>[JsonStringEnumMemberName]</c>.</summary>
    internal bool IsContract { get; }

    /// <summary>Whether the enum carries <c>[Flags]</c> and therefore accepts comma-separated combinations.</summary>
    internal bool IsFlags => _isFlags;

    /// <summary>The public names, in declaration order.</summary>
    internal IReadOnlyList<string> PublicNames { get; }

    /// <summary>The C# names of the members that carry no <c>[JsonStringEnumMemberName]</c>.</summary>
    internal IReadOnlyList<string> UnannotatedMembers { get; }

    /// <summary>The public names joined for use in error messages.</summary>
    internal string AllowedValues { get; }

    /// <summary>Resolves — and validates — the contract of <paramref name="enumType" />.</summary>
    /// <exception cref="EnumContractException">The declared contract is ambiguous or malformed.</exception>
    internal static EnumContract For(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);
        if (!enumType.IsEnum) { throw new ArgumentException($"'{enumType.FullName}' is not an enum.", nameof(enumType)); }

        return Cache.GetOrAdd(enumType, static type => new EnumContract(type));
    }

    /// <summary>Parses a public name into its enum value.</summary>
    /// <remarks>
    /// Whitespace handling mirrors <c>System.Text.Json</c>, which was characterized rather than
    /// assumed: the value as a whole is trimmed, each element of a <c>[Flags]</c> list is trimmed,
    /// and a single trailing comma is tolerated while a leading or repeated one is not.
    /// </remarks>
    internal bool TryParse(string value, out object result) {
        ReadOnlySpan<char> trimmed = value.AsSpan().Trim();

        if (trimmed.IsEmpty) {
            result = null!;

            return false;
        }

        if (_isFlags && trimmed.Contains(',')) {
            return TryParseFlags(trimmed, out result);
        }

        return TryParseSingle(trimmed.ToString(), out result);
    }

    /// <summary>Renders an enum value as its public name, or <see langword="null" /> if it has none.</summary>
    /// <remarks>
    /// A declared member is answered from the cache. A <c>[Flags]</c> combination is handed to
    /// <c>System.Text.Json</c> itself: it decomposes a value by sorting members topologically, so
    /// that a combination covering several bits is preferred over its constituents, and the
    /// tie-breaking between incomparable members is not something worth reimplementing from
    /// observation — two independent shapes were enough to rule out the two obvious rules. Parity
    /// here is by construction rather than by imitation.
    /// </remarks>
    internal string? Format(object value) {
        if (_names.TryGetValue(value, out string? name)) { return name; }
        if (!_isFlags) { return null; }

        try {
            return JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value, EnumType, _writeOptions));
        } catch (JsonException) {
            return null;
        }
    }

    private bool TryParseSingle(string token, out object result) {
        if (_byContractName.TryGetValue(token, out object? contract)) {
            result = contract;

            return true;
        }

        if (_byClrName.TryGetValue(token, out object? clr)) {
            result = clr;

            return true;
        }

        result = null!;

        return false;
    }

    private bool TryParseFlags(ReadOnlySpan<char> value, out object result) {
        result = null!;

        int count = 0;
        foreach (Range _ in value.Split(',')) { count++; }

        ulong accumulator = 0;
        int   index       = 0;

        foreach (Range range in value.Split(',')) {
            index++;
            ReadOnlySpan<char> token = value[range].Trim();

            if (token.IsEmpty) {
                // "read," parses; ",read" and "read,,write" do not.
                if (index == count) { continue; }

                return false;
            }

            if (!TryParseSingle(token.ToString(), out object part)) { return false; }

            accumulator |= ToUInt64(part);
        }

        result = Enum.ToObject(EnumType, accumulator);

        return true;
    }

    private static ulong ToUInt64(object value) {
        Type underlying = Enum.GetUnderlyingType(value.GetType());

        return Type.GetTypeCode(underlying) switch {
            TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 =>
                unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            _ => Convert.ToUInt64(value, CultureInfo.InvariantCulture)
        };
    }

}
