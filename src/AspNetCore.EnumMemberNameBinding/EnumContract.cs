using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
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
    private readonly (object Value, string Name)[] _ordered;
    private readonly bool _isFlags;

    private EnumContract(Type enumType) {
        EnumType = enumType;
        _isFlags = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);

        Dictionary<string, object> byContractName = new(StringComparer.Ordinal);
        Dictionary<string, object> byClrName      = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<object, string> names          = [];
        List<(object, string)>     ordered        = [];
        List<string>               problems       = [];

        foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static)) {
            object value = field.GetValue(null)!;
            JsonStringEnumMemberNameAttribute? attribute = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();

            if (attribute is null) {
                byClrName.TryAdd(field.Name, value);
                names.TryAdd(value, field.Name);
                ordered.Add((value, field.Name));
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
        }

        if (problems.Count > 0) {
            throw new EnumContractException(enumType, problems);
        }

        _byContractName = byContractName.ToFrozenDictionary(StringComparer.Ordinal);
        _byClrName      = byClrName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _names          = names.ToFrozenDictionary();
        _ordered        = [.. ordered];
        AllowedValues   = string.Join(", ", ordered.Select(static o => o.Item2));
    }

    /// <summary>The described enum type.</summary>
    internal Type EnumType { get; }

    /// <summary>Whether at least one member carries <c>[JsonStringEnumMemberName]</c>.</summary>
    internal bool IsContract { get; }

    /// <summary>The public names, in declaration order, for use in error messages and OpenAPI schemas.</summary>
    internal string AllowedValues { get; }

    /// <summary>Resolves — and validates — the contract of <paramref name="enumType" />.</summary>
    /// <exception cref="EnumContractException">The declared contract is ambiguous or malformed.</exception>
    internal static EnumContract For(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);
        if (!enumType.IsEnum) { throw new ArgumentException($"'{enumType.FullName}' is not an enum.", nameof(enumType)); }

        return Cache.GetOrAdd(enumType, static type => new EnumContract(type));
    }

    /// <summary>Parses a public name into its enum value.</summary>
    internal bool TryParse(string value, out object result) {
        if (_isFlags && value.Contains(',', StringComparison.Ordinal)) {
            return TryParseFlags(value, out result);
        }

        return TryParseSingle(value, out result);
    }

    /// <summary>Renders an enum value as its public name, or <see langword="null" /> if it has none.</summary>
    internal string? Format(object value) {
        if (_names.TryGetValue(value, out string? name)) { return name; }
        if (!_isFlags) { return null; }

        ulong remaining = ToUInt64(value);
        if (remaining == 0) { return null; }

        List<string> parts = [];
        foreach ((object memberValue, string memberName) in _ordered) {
            ulong bits = ToUInt64(memberValue);
            if (bits != 0 && (remaining & bits) == bits) {
                remaining &= ~bits;
                parts.Add(memberName);
            }
        }

        return remaining == 0 && parts.Count > 0 ? string.Join(", ", parts) : null;
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

    private bool TryParseFlags(string value, out object result) {
        ulong accumulator = 0;

        foreach (Range range in value.AsSpan().Split(',')) {
            string token = value.AsSpan(range).Trim().ToString();
            if (!TryParseSingle(token, out object part)) {
                result = null!;

                return false;
            }

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
