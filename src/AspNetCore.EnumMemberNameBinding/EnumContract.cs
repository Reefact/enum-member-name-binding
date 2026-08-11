using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
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
///   <item>a comma-separated list of the above is accepted on any enum, and the values are combined bitwise.</item>
/// </list>
/// Numeric values are never accepted — the equivalent of <c>allowIntegerValues: false</c>.
/// <para>
/// The comma is not reserved to <c>[Flags]</c>, which reads as though it should be. Neither
/// <c>Enum.Parse</c> nor <c>System.Text.Json</c> looks at the attribute before splitting, so a list
/// is accepted on an ordinary enum too — and refusing it here would make a registered enum stricter
/// than the same enum left alone, which is the one thing this package promises never to do. What
/// <c>[Flags]</c> still decides is whether ASP.NET Core will bind the result: see
/// <c>docs/for-users/limitations.en.md</c>.
/// </para>
/// <para>
/// It decides one further thing, which the same reasoning had got backwards: whether a comma may
/// appear <em>inside</em> a declared name. The serializer refuses that on a <c>[Flags]</c> enum and
/// accepts it anywhere else, because it looks the whole value up as a name before splitting — so
/// <c>"news,world"</c> both writes and reads back on an ordinary enum. This package refused it
/// everywhere, which was the promise above broken by the rule meant to serve it. See
/// <see cref="TryParse" /> for the order that makes it work, and <c>EMN0004</c> for the build-time
/// half.
/// </para>
/// </remarks>
internal sealed class EnumContract {

    private static readonly ConcurrentDictionary<Type, EnumContract> Cache = new();

    private readonly FrozenDictionary<string, object> _byContractName;
    private readonly FrozenDictionary<string, object> _byClrName;
    private readonly FrozenDictionary<string, object> _byClrNameIgnoringCase;
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
        Dictionary<string, object> byClrName      = new(StringComparer.Ordinal);
        List<(object, string)>     ordered        = [];
        List<string>               problems       = [];
        List<string>               unannotated    = [];
        Dictionary<string, string> declaredBy     = new(StringComparer.Ordinal);

        // Keyed by member and not by value, because which of two members sharing a value owns the
        // name is not decided here — it is decided after the loop, in the order the serializer
        // decides it. See NamesByValue.
        Dictionary<string, (object Value, string Name)> byMember = new(StringComparer.Ordinal);

        foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static)) {
            object value = field.GetValue(null)!;
            JsonStringEnumMemberNameAttribute? attribute = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();

            if (attribute is null) {
                // Ordinal, so two members differing only by case each keep their own entry. C# will
                // not let two members share an exact name, so nothing can be claimed twice here.
                byClrName[field.Name] = value;
                byMember[field.Name] = (value, field.Name);
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

            byMember[field.Name] = (value, name);
            ordered.Add((value, name));
            declaredBy[name] = field.Name;
        }

        AddShadowingProblems(declaredBy, unannotated, problems);

        if (problems.Count > 0) { throw new EnumContractException(enumType, problems); }

        _byContractName = byContractName.ToFrozenDictionary(StringComparer.Ordinal);
        _byClrName      = byClrName.ToFrozenDictionary(StringComparer.Ordinal);
        _names          = NamesByValue(enumType, byMember);
        _byClrNameIgnoringCase = ClrNamesIgnoringCase(enumType, byClrName, _isFlags);
        PublicNames         = [.. ordered.Select(static o => o.Item2)];
        UnannotatedMembers  = [.. unannotated];
    }

    /// <summary>
    /// The unannotated members again, reachable under any casing — the fallback that runs when no
    /// member answers to the token exactly.
    /// </summary>
    /// <remarks>
    /// One dictionary with a loose comparer cannot do both halves, and reading it as though it could
    /// is what lost a member: <c>Read</c> and <c>read</c> collide under
    /// <see cref="StringComparer.OrdinalIgnoreCase" />, so the second was dropped and the token
    /// naming it exactly resolved to the first. <c>System.Text.Json</c> answers <c>read</c> with the
    /// member spelled that way, so the query string and the request body disagreed on one word.
    /// <para>
    /// Which member a casing that matches none of them exactly resolves to is not this package's to
    /// choose either, and the answer is not the same on both kinds of enum. The serializer walks its
    /// members in the order it holds them and keeps the first it meets: on an ordinary enum that is
    /// <see cref="Enum.GetNames(Type)" /> order, which is neither declaration order nor the
    /// arithmetic one; on a <c>[Flags]</c> one the members are held with the most bits first, so a
    /// composite wins over a member it covers. Twelve shapes were measured against
    /// <c>JsonSerializer</c> to establish it — <c>ShadowedMemberTests</c> holds the ordinary ones and
    /// <c>ReadParityTests</c> compares the <c>[Flags]</c> ones token by token.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    private static FrozenDictionary<string, object> ClrNamesIgnoringCase(Type enumType, Dictionary<string, object> byClrName, bool isFlags) {
        Dictionary<string, object> ignoringCase = new(StringComparer.OrdinalIgnoreCase);

        // The members carrying an attribute are in GetNames and not in byClrName: their C# name is
        // replaced by the declared one, so it is not a name this enum answers to at all.
        foreach (string member in FallbackOrder(enumType, byClrName, isFlags)) {
            if (!byClrName.TryGetValue(member, out object? value)) { continue; }

            ignoringCase.TryAdd(member, value);
        }

        return ignoringCase.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The members in the order the serializer meets them, which is what decides a case-insensitive
    /// tie.
    /// </summary>
    /// <remarks>
    /// <see cref="Enum.GetNames(Type)" /> for an ordinary enum, and bit count descending for a
    /// <c>[Flags]</c> one. <c>OrderByDescending</c> is a stable sort, so members tied on bit count
    /// keep the <c>GetNames</c> order between them — which is what the serializer does, measured on
    /// three shapes where the tie is the only thing left to decide, including one declared so that
    /// declaration order and <c>GetNames</c> order disagree.
    /// <para>
    /// The count is taken over the <em>sign-extended</em> <see cref="ulong" /> that
    /// <see cref="ToUInt64" /> already produces, which is the one thing here worth measuring rather
    /// than reasoning about: <c>-128</c> on an <c>sbyte</c> enum sets one bit of the byte and
    /// fifty-seven of the widened value, and the serializer counts fifty-seven — it answers a miscased
    /// token with that member over one setting two bits, whichever order the two are declared in.
    /// Counting the byte would have been the tidier reading and the wrong one.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    private static IEnumerable<string> FallbackOrder(Type enumType, Dictionary<string, object> byClrName, bool isFlags) {
        string[] names = Enum.GetNames(enumType);
        if (!isFlags) { return names; }

        return names.OrderByDescending(member => byClrName.TryGetValue(member, out object? value) ? BitOperations.PopCount(ToUInt64(value)) : 0);
    }

    /// <summary>
    /// Value to public name: what <see cref="Format" /> answers for a declared member.
    /// </summary>
    /// <remarks>
    /// Two members may share a numeric value, and only one of them can be the name it is written
    /// back as. Which one is not this package's to choose — it has to be the one
    /// <c>System.Text.Json</c> writes, or the same application answers a value with two names: the
    /// response body says <c>shipped</c> while a link built through
    /// <see cref="EnumMemberNames.GetPublicName" /> says <c>in_transit</c>.
    /// <para>
    /// The serializer walks the members in <see cref="Enum.GetNames(Type)" /> order and keeps the
    /// first it meets for a value, so reading that order here makes the two agree by construction
    /// rather than by imitation — the argument <see cref="Format" /> already makes when it hands a
    /// combination to the serializer itself.
    /// </para>
    /// <para>
    /// Declaration order is what this read, and it is not the same order: <c>GetNames</c> sorts by
    /// the binary value, and among members sharing one it does not keep the order they were written
    /// in. Seven shapes were measured against <c>JsonSerializer</c> and three disagreed, so this is
    /// characterized rather than assumed; <c>FormattingParityTests</c> holds all of them. Reading it
    /// once here rather than at each call is what keeps <see cref="Format" /> a lookup.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    private static FrozenDictionary<object, string> NamesByValue(Type enumType, Dictionary<string, (object Value, string Name)> byMember) {
        Dictionary<object, string> names = [];

        // Every member is in byMember: the only path that skips one collects a problem, and a
        // contract with problems threw above rather than reaching here.
        foreach (string member in Enum.GetNames(enumType)) {
            (object Value, string Name) declared = byMember[member];

            names.TryAdd(declared.Value, declared.Name);
        }

        return names.ToFrozenDictionary();
    }

    /// <summary>
    /// Why a declared name is malformed on its own terms, or <c>null</c>. The tests run in this order
    /// because each assumes the ones before it passed — an empty name has no first character to
    /// inspect.
    /// </summary>
    /// <remarks>
    /// The comma is the one test that reads <paramref name="isFlags" />, because it is the one the
    /// serializer scopes that way: it refuses a comma in a declared name on a <c>[Flags]</c> enum and
    /// accepts it on any other, which its own message says out loud — "Flags enums must
    /// <em>additionally</em> not contain commas". Refusing it everywhere made a registered enum
    /// stricter than the same enum left alone, on the strength of a claim about the serializer that
    /// turned out to be false.
    /// </remarks>
    private static string? MalformedNameProblem(string memberName, string name, bool isFlags) {
        if (string.IsNullOrEmpty(name)) { return Problem.EmptyName(memberName); }
        if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1])) { return Problem.SurroundingWhitespace(memberName, name); }
        if (isFlags && name.Contains(',', StringComparison.Ordinal)) { return Problem.CommaInFlagsName(memberName, name); }

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
                   "On a [Flags] enum a comma separates the values of a combination, so a name containing one cannot be read back.";
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

    /// <summary>Whether the enum carries <c>[Flags]</c>.</summary>
    /// <remarks>
    /// Not what decides whether a comma-separated combination is <em>accepted</em>: one is accepted on
    /// every enum, for the reason given on this class. What the attribute decides is that the set of
    /// bindable values is open rather than closed — which is what lets <see cref="Format" /> write a
    /// combination back out, and the OpenAPI companion describe the schema with a pattern rather
    /// than a closed list.
    /// <para>
    /// Open is not unbounded, and reading it that way is what put a bug in the binder: a combination
    /// decomposing into no declared member is refused off the body like any other undefined value,
    /// which two declared composites that overlap can produce. <c>EnumMemberNameModelBinder</c> holds
    /// that, and <c>docs/for-users/limitations.en.md</c> writes it down.
    /// </para>
    /// </remarks>
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

    /// <summary>Parses a public name, or a comma-separated list of them, into its enum value.</summary>
    /// <remarks>
    /// Whitespace handling mirrors <c>System.Text.Json</c>, which was characterized rather than
    /// assumed: the value as a whole is trimmed, each element of a list is trimmed, and a single
    /// trailing comma is tolerated while a leading or repeated one is not.
    /// <para>
    /// So does the order of the two attempts, and it is the whole reason a comma may appear inside a
    /// declared name off <c>[Flags]</c>: the serializer looks the trimmed value up as one name
    /// before it splits anything. On an enum declaring <c>a</c>, <c>b</c> and <c>a,b</c> it answers
    /// <c>"a,b"</c> with the member of that name rather than with <c>a | b</c> — while <c>"a, b"</c>,
    /// which no name spells, falls through to the split and is the combination. Both were measured.
    /// </para>
    /// </remarks>
    internal bool TryParse(string value, [MaybeNullWhen(false)] out object result) {
        ArgumentNullException.ThrowIfNull(value);

        ReadOnlySpan<char> trimmed = value.AsSpan().Trim();

        if (trimmed.IsEmpty) {
            result = null;

            return false;
        }

        if (TryParseSingle(trimmed.ToString(), out result)) { return true; }
        if (!trimmed.Contains(',')) { return false; }

        return TryParseList(trimmed, out result);
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

    /// <summary>
    /// A value with no comma in it. The exact spelling of an unannotated member wins here, and only
    /// here — see <see cref="TryParseListItem" />.
    /// </summary>
    private bool TryParseSingle(string token, [MaybeNullWhen(false)] out object result) {
        if (_byContractName.TryGetValue(token, out object? contract)) {
            result = contract;

            return true;
        }

        if (_byClrName.TryGetValue(token, out object? clr)) {
            result = clr;

            return true;
        }

        return TryParseIgnoringCase(token, out result);
    }

    /// <summary>
    /// One token of a comma-separated list, where an unannotated member is matched ignoring case and
    /// nothing else — the exact spelling is not preferred.
    /// </summary>
    /// <remarks>
    /// The asymmetry is <c>System.Text.Json</c>'s and was characterized rather than reasoned about:
    /// a value carrying a comma resolves each of its parts through one case-insensitive lookup, while
    /// a value carrying none prefers an exact match first. A single trailing comma is enough to move
    /// a value from the second rule to the first — on <c>{ Read = 2, read = 4 }</c> the serializer
    /// reads <c>"read"</c> as 4 and <c>"read,"</c> as 2.
    /// <para>
    /// Reading both paths the same way is what this package did until <c>ReadParityTests</c> was
    /// written. Preferring the exact spelling everywhere was an improvement on the single value and a
    /// regression on the list, so the divergence moved rather than closing: <c>"read,one"</c> bound
    /// 5 where the request body binds 3. Declared names are unaffected — they are ordinal in both.
    /// </para>
    /// </remarks>
    private bool TryParseListItem(string token, [MaybeNullWhen(false)] out object result) {
        if (_byContractName.TryGetValue(token, out object? contract)) {
            result = contract;

            return true;
        }

        return TryParseIgnoringCase(token, out result);
    }

    private bool TryParseIgnoringCase(string token, [MaybeNullWhen(false)] out object result) {
        if (_byClrNameIgnoringCase.TryGetValue(token, out object? ignoringCase)) {
            result = ignoringCase;

            return true;
        }

        result = null;

        return false;
    }

    private bool TryParseList(ReadOnlySpan<char> value, [MaybeNullWhen(false)] out object result) {
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

            if (!TryParseListItem(token.ToString(), out object? part)) { return false; }

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
