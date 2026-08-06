using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// Writing a value out must produce exactly what <c>System.Text.Json</c> writes, for the same reason
/// parsing must accept exactly what it accepts. The oracle is the serializer itself, never a
/// hand-written expectation.
/// </summary>
public sealed class FormattingParityTests {

    public enum NumericAliases {

        [JsonStringEnumMemberName("first")]  First  = 1,
        [JsonStringEnumMemberName("uno")]    Uno    = 1,
        [JsonStringEnumMemberName("second")] Second = 2

    }

    /// <summary>The combination is declared before the members it is made of.</summary>
    [Flags]
    public enum CompositeDeclaredFirst {

        [JsonStringEnumMemberName("all")]   All   = 3,
        [JsonStringEnumMemberName("read")]  Read  = 1,
        [JsonStringEnumMemberName("write")] Write = 2

    }

    /// <summary>The same shape, declared the other way round.</summary>
    [Flags]
    public enum CompositeDeclaredLast {

        [JsonStringEnumMemberName("read")]  Read  = 1,
        [JsonStringEnumMemberName("write")] Write = 2,
        [JsonStringEnumMemberName("all")]   All   = 3

    }

    /// <summary>7 can be written as three members, or as two, in more than one way.</summary>
    [Flags]
    public enum OverlappingComposites {

        [JsonStringEnumMemberName("read")]         Read        = 1,
        [JsonStringEnumMemberName("write")]        Write       = 2,
        [JsonStringEnumMemberName("read_write")]   ReadWrite   = 3,
        [JsonStringEnumMemberName("delete")]       Delete      = 4,
        [JsonStringEnumMemberName("write_delete")] WriteDelete = 6

    }

    public enum SignedValues {

        [JsonStringEnumMemberName("negative")] Negative = -1,
        [JsonStringEnumMemberName("zero")]     Zero     = 0,
        [JsonStringEnumMemberName("positive")] Positive = 1

    }

    [Flags]
    public enum SignedFlags : sbyte {

        [JsonStringEnumMemberName("a")]    A    = 1,
        [JsonStringEnumMemberName("b")]    B    = 2,
        [JsonStringEnumMemberName("high")] High = -128

    }

    [Flags]
    public enum UnsignedFlags : ulong {

        [JsonStringEnumMemberName("low")]  Low  = 1,
        [JsonStringEnumMemberName("high")] High = 1UL << 63

    }

    [Flags]
    public enum WithZeroMember {

        [JsonStringEnumMemberName("none")]  None  = 0,
        [JsonStringEnumMemberName("read")]  Read  = 1,
        [JsonStringEnumMemberName("write")] Write = 2

    }

    [Theory]
    [InlineData(typeof(NumericAliases))]
    [InlineData(typeof(CompositeDeclaredFirst))]
    [InlineData(typeof(CompositeDeclaredLast))]
    [InlineData(typeof(OverlappingComposites))]
    [InlineData(typeof(SignedValues))]
    [InlineData(typeof(SignedFlags))]
    [InlineData(typeof(UnsignedFlags))]
    [InlineData(typeof(WithZeroMember))]
    public void every_value_is_written_exactly_as_system_text_json_writes_it(Type enumType) {
        EnumContract contract = EnumContract.For(enumType);
        JsonSerializerOptions oracle = OracleFor(enumType);

        List<string> divergences = [];

        foreach (object value in CandidateValues(enumType)) {
            string? expected = WriteWithSystemTextJson(value, enumType, oracle);
            string? actual   = contract.Format(value);

            if (!string.Equals(expected, actual, StringComparison.Ordinal)) {
                divergences.Add($"{Describe(value)}: System.Text.Json writes {Show(expected)}, this library writes {Show(actual)}");
            }
        }

        Assert.True(divergences.Count == 0,
                    $"{enumType.Name} diverges on {divergences.Count} value(s):{Environment.NewLine}"
                  + string.Join(Environment.NewLine, divergences));
    }

    /// <summary>Whatever is written must be readable again, and yield the value it came from.</summary>
    [Theory]
    [InlineData(typeof(NumericAliases))]
    [InlineData(typeof(CompositeDeclaredFirst))]
    [InlineData(typeof(CompositeDeclaredLast))]
    [InlineData(typeof(OverlappingComposites))]
    [InlineData(typeof(SignedValues))]
    [InlineData(typeof(SignedFlags))]
    [InlineData(typeof(UnsignedFlags))]
    [InlineData(typeof(WithZeroMember))]
    public void what_is_written_reads_back_to_the_same_value(Type enumType) {
        EnumContract contract = EnumContract.For(enumType);

        foreach (object value in CandidateValues(enumType)) {
            string? written = contract.Format(value);
            if (written is null) { continue; }

            Assert.True(contract.TryParse(written, out object read), $"'{written}' was written but cannot be read back.");
            Assert.Equal(value, read);
        }
    }

    private static JsonSerializerOptions OracleFor(Type enumType) {
        Type converterType = typeof(JsonStringEnumConverter<>).MakeGenericType(enumType);

        return new JsonSerializerOptions {
            Converters = { (JsonConverter)Activator.CreateInstance(converterType, null, false)! }
        };
    }

    private static string? WriteWithSystemTextJson(object value, Type enumType, JsonSerializerOptions oracle) {
        try {
            return JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value, enumType, oracle));
        } catch (JsonException) {
            return null;
        }
    }

    /// <summary>Every declared value, every subset of the declared bits, and a few undeclared ones.</summary>
    private static HashSet<object> CandidateValues(Type enumType) {
        HashSet<object> values = [.. Enum.GetValues(enumType).Cast<object>()];

        if (enumType.IsDefined(typeof(FlagsAttribute), inherit: false)) {
            ulong union = 0;
            foreach (object declared in Enum.GetValues(enumType)) { union |= ToUInt64(declared); }

            ulong[] bits = [.. Enumerable.Range(0, 64).Select(offset => 1UL << offset).Where(bit => (union & bit) != 0)];
            Assert.True(bits.Length <= 16, $"{enumType.Name} declares {bits.Length} distinct bits; the subset enumeration would explode.");

            for (int mask = 0; mask < 1 << bits.Length; mask++) {
                ulong combination = 0;
                for (int index = 0; index < bits.Length; index++) {
                    if ((mask & (1 << index)) != 0) { combination |= bits[index]; }
                }

                values.Add(Enum.ToObject(enumType, combination));
            }
        }

        // Undeclared values, which have no public name at all.
        foreach (long candidate in new long[] { 42, 99 }) {
            try { values.Add(Enum.ToObject(enumType, Convert.ChangeType(candidate, Enum.GetUnderlyingType(enumType), CultureInfo.InvariantCulture))); }
            catch (OverflowException) { /* does not fit the underlying type */ }
        }

        return values;
    }

    /// <summary>
    /// Masked to the width of the underlying type. Sign-extending an sbyte's -128 to 64 bits would
    /// make the union of declared bits look 57 bits wide, and the subset enumeration below explode.
    /// </summary>
    private static ulong ToUInt64(object value) {
        return Type.GetTypeCode(Enum.GetUnderlyingType(value.GetType())) switch {
            TypeCode.SByte => (byte)Convert.ToSByte(value, CultureInfo.InvariantCulture),
            TypeCode.Int16 => (ushort)Convert.ToInt16(value, CultureInfo.InvariantCulture),
            TypeCode.Int32 => (uint)Convert.ToInt32(value, CultureInfo.InvariantCulture),
            TypeCode.Int64 => unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            _              => Convert.ToUInt64(value, CultureInfo.InvariantCulture)
        };
    }

    private static string Describe(object value) {
        return $"{value} ({Convert.ToString(Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)})";
    }

    private static string Show(string? value) => value is null ? "<nothing>" : $"'{value}'";

}
