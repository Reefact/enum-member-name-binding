using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Built on first use, and only ever by <see cref="Format" /> for a <c>[Flags]</c> combination
    /// that is not itself a declared member. Reading the public names — for an OpenAPI schema, say —
    /// never needs it, and neither does an enum that declares no contract.
    /// </summary>
    private JsonSerializerOptions? _writeOptions;

    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
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

            string? problem = MalformedNameProblem(field.Name, name, _isFlags);

            // The duplicate test claims the name as it checks it, so it stays with the collection it
            // claims from rather than moving into a function that would have to be handed it.
            if (problem is null && !byContractName.TryAdd(name, value)) {
                problem = Problem.DuplicateName(field.Name, name);
            }

            if (problem is not null) {
                problems.Add(problem);
                continue;
            }

            names.TryAdd(value, name);
            ordered.Add((value, name));
            declaredBy[name] = field.Name;
        }

        AddShadowingProblems(declaredBy, unannotated, problems);

        if (problems.Count > 0) {
            throw new EnumContractException(enumType, problems);
        }

        _byContractName = byContractName.ToFrozenDictionary(StringComparer.Ordinal);
        _byClrName      = byClrName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _names          = names.ToFrozenDictionary();
        PublicNames         = [.. ordered.Select(static o => o.Item2)];
        UnannotatedMembers  = [.. unannotated];
        AllowedValues       = string.Join(", ", PublicNames);
    }

    /// <summary>
    /// Why a declared name is malformed on its own terms, or <c>null</c>. The tests run in this order
    /// because each assumes the ones before it passed — an empty name has no first character to
    /// inspect.
    /// </summary>
    private static string? MalformedNameProblem(string memberName, string name, bool isFlags) {
        if (string.IsNullOrEmpty(name)) {
            return Problem.EmptyName(memberName);
        }

        if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1])) {
            return Problem.SurroundingWhitespace(memberName, name);
        }

        if (isFlags && name.Contains(',', StringComparison.Ordinal)) {
            return Problem.CommaInFlagsName(memberName, name);
        }

        return null;
    }

    /// <summary>
    /// Reports every declared name that is also the C# name of an unannotated member.
    /// </summary>
    /// <remarks>
    /// A declared name is matched before an unannotated member's C# name, and case-sensitively, so
    /// the shadowed member ends up answering to every casing of its name except its own. The
    /// comparison is case-insensitive because that is how the C# names are looked up.
    /// </remarks>
    private static void AddShadowingProblems(Dictionary<string, string> declaredBy, List<string> unannotated, List<string> problems) {
        foreach (KeyValuePair<string, string> declared in declaredBy) {
            string? shadowed = unannotated.Find(member => string.Equals(member, declared.Key, StringComparison.OrdinalIgnoreCase));
            if (shadowed is null) { continue; }

            problems.Add(Problem.ShadowsAnotherMember(declared.Value, declared.Key, shadowed));
        }
    }

    /// <summary>
    /// What this type says when a declared contract cannot be applied. One entry of
    /// <see cref="EnumContractException.Problems" /> each.
    /// </summary>
    private static class Problem {

        internal static string EmptyName(string memberName) {
            return $"member '{memberName}' declares an empty name.";
        }

        internal static string SurroundingWhitespace(string memberName, string name) {
            return $"member '{memberName}' declares the name '{name}', which has leading or trailing whitespace.";
        }

        internal static string CommaInFlagsName(string memberName, string name) {
            return $"member '{memberName}' declares the name '{name}', which contains a comma. " +
                   "A comma separates values in a [Flags] enum and cannot appear inside a name.";
        }

        internal static string DuplicateName(string memberName, string name) {
            return $"member '{memberName}' declares the name '{name}', which is already declared by another member. " +
                   "Two members cannot share the same public name.";
        }

        internal static string ShadowsAnotherMember(string memberName, string name, string shadowedMemberName) {
            return $"member '{memberName}' declares the public name '{name}', which is also the C# name " +
                   $"of member '{shadowedMemberName}'. The value '{name}' resolves to '{memberName}', leaving " +
                   $"'{shadowedMemberName}' reachable only through a different casing. Rename the public name, or annotate " +
                   $"'{shadowedMemberName}' as well.";
        }

    }

    /// <summary>The described enum type.</summary>
    internal Type EnumType { get; }

    /// <summary>Whether at least one member carries <c>[JsonStringEnumMemberName]</c>.</summary>
    internal bool IsContract { get; }

    /// <summary>Whether the enum carries <c>[Flags]</c> and therefore accepts comma-separated combinations.</summary>
    internal bool IsFlags => _isFlags;

    /// <summary>The public names, in declaration order.</summary>
    /// <remarks>
    /// Immutable by type, not by convention. These lists are cached and handed to callers — including
    /// through the public <see cref="EnumMemberNames" /> API — so the guarantee cannot rest on a
    /// collection expression happening to synthesize a read-only wrapper rather than an array.
    /// </remarks>
    internal ImmutableArray<string> PublicNames { get; }

    /// <summary>The C# names of the members that carry no <c>[JsonStringEnumMemberName]</c>.</summary>
    internal ImmutableArray<string> UnannotatedMembers { get; }

    /// <summary>The public names joined for use in error messages.</summary>
    internal string AllowedValues { get; }

    /// <summary>Resolves — and validates — the contract of <paramref name="enumType" />.</summary>
    /// <exception cref="EnumContractException">The declared contract is ambiguous or malformed.</exception>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    internal static EnumContract For(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);
        if (!enumType.IsEnum) { throw new ArgumentException($"'{enumType.FullName}' is not an enum.", nameof(enumType)); }

        if (Cache.TryGetValue(enumType, out EnumContract? cached)) { return cached; }

        // Built outside GetOrAdd so the annotation on enumType survives; a concurrent build is
        // benign, since the descriptor is immutable and only one instance is ever published.
        return Cache.GetOrAdd(enumType, new EnumContract(enumType));
    }

    /// <summary>Parses a public name into its enum value.</summary>
    /// <remarks>
    /// Whitespace handling mirrors <c>System.Text.Json</c>, which was characterized rather than
    /// assumed: the value as a whole is trimmed, each element of a <c>[Flags]</c> list is trimmed,
    /// and a single trailing comma is tolerated while a leading or repeated one is not.
    /// </remarks>
    internal bool TryParse(string value, [MaybeNullWhen(false)] out object result) {
        ArgumentNullException.ThrowIfNull(value);

        ReadOnlySpan<char> trimmed = value.AsSpan().Trim();

        if (trimmed.IsEmpty) {
            result = null;

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
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    [RequiresDynamicCode(TrimmingMessages.DynamicCode)]
    internal string? Format(object value) {
        ArgumentNullException.ThrowIfNull(value);

        if (_names.TryGetValue(value, out string? name)) { return name; }
        if (!_isFlags) { return null; }

        // A benign race: two threads may each build one, and the two are equivalent.
        JsonSerializerOptions options = _writeOptions ??= CreateWriteOptions(EnumType);

        try {
            return JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value, EnumType, options));
        } catch (JsonException) {
            return null;
        }
    }

    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    [RequiresDynamicCode(TrimmingMessages.DynamicCode)]
    private static JsonSerializerOptions CreateWriteOptions(Type enumType) {
        Type converterType = typeof(JsonStringEnumConverter<>).MakeGenericType(enumType);

        return new JsonSerializerOptions {
            Converters = { (JsonConverter)Activator.CreateInstance(converterType, null, false)! }
        };
    }

    private bool TryParseSingle(string token, [MaybeNullWhen(false)] out object result) {
        if (_byContractName.TryGetValue(token, out object? contract)) {
            result = contract;

            return true;
        }

        if (_byClrName.TryGetValue(token, out object? clr)) {
            result = clr;

            return true;
        }

        result = null;

        return false;
    }

    private bool TryParseFlags(ReadOnlySpan<char> value, [MaybeNullWhen(false)] out object result) {
        result = null;

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

            if (!TryParseSingle(token.ToString(), out object? part)) { return false; }

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
