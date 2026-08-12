using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Tests;

// One [Flags] contract per underlying type C# allows, each carrying the value that type is awkward
// about: the most negative value for a signed type, the top bit for an unsigned one. Those are what
// the parser's accumulator has to survive — it widens every member to ulong, ORs them, and hands the
// result to Enum.ToObject to be narrowed again.
[Flags] public enum ByteScopes : byte { [JsonStringEnumMemberName("a")] A = 1, [JsonStringEnumMemberName("b")] B = 2, [JsonStringEnumMemberName("extreme")] Extreme = 128 }
[Flags] public enum SByteScopes : sbyte { [JsonStringEnumMemberName("a")] A = 1, [JsonStringEnumMemberName("b")] B = 2, [JsonStringEnumMemberName("extreme")] Extreme = sbyte.MinValue }
[Flags] public enum ShortScopes : short { [JsonStringEnumMemberName("a")] A = 1, [JsonStringEnumMemberName("b")] B = 2, [JsonStringEnumMemberName("extreme")] Extreme = short.MinValue }
[Flags] public enum UShortScopes : ushort { [JsonStringEnumMemberName("a")] A = 1, [JsonStringEnumMemberName("b")] B = 2, [JsonStringEnumMemberName("extreme")] Extreme = 1 << 15 }
[Flags] public enum IntScopes : int { [JsonStringEnumMemberName("a")] A = 1, [JsonStringEnumMemberName("b")] B = 2, [JsonStringEnumMemberName("extreme")] Extreme = int.MinValue }
[Flags] public enum UIntScopes : uint { [JsonStringEnumMemberName("a")] A = 1, [JsonStringEnumMemberName("b")] B = 2, [JsonStringEnumMemberName("extreme")] Extreme = 1U << 31 }
[Flags] public enum LongScopes : long { [JsonStringEnumMemberName("a")] A = 1, [JsonStringEnumMemberName("b")] B = 2, [JsonStringEnumMemberName("extreme")] Extreme = long.MinValue }
[Flags] public enum ULongScopes : ulong { [JsonStringEnumMemberName("a")] A = 1, [JsonStringEnumMemberName("b")] B = 2, [JsonStringEnumMemberName("extreme")] Extreme = 1UL << 63 }

/// <summary>
/// The same awkwardness without <c>[Flags]</c>, where a parsed value must also be one
/// <see cref="Enum.IsDefined(Type, object)" /> recognises.
/// </summary>
public enum SignedStep : sbyte {

    [JsonStringEnumMemberName("down")] Down = sbyte.MinValue,
    [JsonStringEnumMemberName("zero")] Zero = 0,
    [JsonStringEnumMemberName("up")]   Up   = sbyte.MaxValue

}

// Top level on purpose: MVC's controller discovery requires Type.IsPublic, which is false for a
// nested type, so a nested controller is silently never routed.
[ApiController]
public sealed class UnderlyingTypeController : ControllerBase {

    [HttpGet("/underlying/byte")]   public IActionResult ByteScopesValue([FromQuery] ByteScopes value) => Ok(new { value = value.ToString() });
    [HttpGet("/underlying/sbyte")]  public IActionResult SByteScopesValue([FromQuery] SByteScopes value) => Ok(new { value = value.ToString() });
    [HttpGet("/underlying/short")]  public IActionResult ShortScopesValue([FromQuery] ShortScopes value) => Ok(new { value = value.ToString() });
    [HttpGet("/underlying/ushort")] public IActionResult UShortScopesValue([FromQuery] UShortScopes value) => Ok(new { value = value.ToString() });
    [HttpGet("/underlying/int")]    public IActionResult IntScopesValue([FromQuery] IntScopes value) => Ok(new { value = value.ToString() });
    [HttpGet("/underlying/uint")]   public IActionResult UIntScopesValue([FromQuery] UIntScopes value) => Ok(new { value = value.ToString() });
    [HttpGet("/underlying/long")]   public IActionResult LongScopesValue([FromQuery] LongScopes value) => Ok(new { value = value.ToString() });
    [HttpGet("/underlying/ulong")]  public IActionResult ULongScopesValue([FromQuery] ULongScopes value) => Ok(new { value = value.ToString() });
    [HttpGet("/underlying/step")]   public IActionResult SignedStepValue([FromQuery] SignedStep value) => Ok(new { value = value.ToString() });

}

/// <summary>
/// The parity matrix, run over every underlying type an <c>enum</c> can have rather than over
/// <c>int</c> alone.
/// </summary>
/// <remarks>
/// The parse widens every member to <c>ulong</c>, ORs them and narrows the result back with
/// <c>Enum.ToObject</c>. Nothing about that is obviously safe for the seven types that are not
/// <c>int</c> — sign extension makes <c>sbyte</c>'s <c>-128</c> a value with fifty-seven bits set,
/// and <c>ulong</c>'s top bit is the one a signed accumulator would lose. The suite that pinned it
/// used <c>ProductStatus</c> and <c>Permissions</c>, both <c>int</c>, so the widest half of the
/// arithmetic was exercised by nothing.
/// <para>
/// The oracle is <c>JsonSerializer</c>, as everywhere else here: no expectation is written down, so
/// the test states parity rather than a belief about what these values should be. It found no
/// divergence when it was written — that is the result, not a reason to skip it, because the claim
/// this package makes is precisely that there is none.
/// </para>
/// </remarks>
[Collection(nameof(UnderlyingTypeCollection))]
public sealed class UnderlyingTypeParityTests(UnderlyingTypeApi api) {

    /// <summary>
    /// Names, casing, combinations of two and three, the whitespace and trailing-comma shapes, and
    /// the numeric forms that must all be refused. The empty value is deliberately absent: ASP.NET
    /// Core settles it before any parse is reached, which is a documented divergence with a suite of
    /// its own in <c>EmptyValueTests</c>.
    /// </summary>
    public static TheoryData<string> Inputs => new() {
        "a", "b", "extreme", "A", "EXTREME", "bogus",
        "a,b", "a, b", " a , b ", "a,extreme", "b,extreme", "a,b,extreme", "extreme,extreme",
        "a,", "a, ", "a,,b", ",a", "a, ,b", "a,bogus",
        "0", "1", "-1", "128", "255"
    };

    /// <summary>
    /// The same, for the enum with no <c>[Flags]</c>. Its combinations are only the ones whose result
    /// is a declared member: <c>down,up</c> is <c>-1</c>, which names none, and that is the one
    /// documented input the body accepts and no other channel does — it has its own test in
    /// <see cref="ParityWithSystemTextJsonTests" />.
    /// </summary>
    public static TheoryData<string> StepInputs => new() {
        "down", "zero", "up", "Down", "UP", "bogus",
        "down,zero", "zero,up", "up,up", " zero , up ", "up,",
        "0", "-128", "127"
    };

    [Theory, MemberData(nameof(Inputs))] public Task a_byte_enum_binds_what_the_body_binds(string input) => Parity<ByteScopes>(input, "byte");
    [Theory, MemberData(nameof(Inputs))] public Task an_sbyte_enum_binds_what_the_body_binds(string input) => Parity<SByteScopes>(input, "sbyte");
    [Theory, MemberData(nameof(Inputs))] public Task a_short_enum_binds_what_the_body_binds(string input) => Parity<ShortScopes>(input, "short");
    [Theory, MemberData(nameof(Inputs))] public Task a_ushort_enum_binds_what_the_body_binds(string input) => Parity<UShortScopes>(input, "ushort");
    [Theory, MemberData(nameof(Inputs))] public Task an_int_enum_binds_what_the_body_binds(string input) => Parity<IntScopes>(input, "int");
    [Theory, MemberData(nameof(Inputs))] public Task a_uint_enum_binds_what_the_body_binds(string input) => Parity<UIntScopes>(input, "uint");
    [Theory, MemberData(nameof(Inputs))] public Task a_long_enum_binds_what_the_body_binds(string input) => Parity<LongScopes>(input, "long");
    [Theory, MemberData(nameof(Inputs))] public Task a_ulong_enum_binds_what_the_body_binds(string input) => Parity<ULongScopes>(input, "ulong");

    [Theory, MemberData(nameof(StepInputs))]
    public Task a_signed_enum_without_flags_binds_what_the_body_binds(string input) => Parity<SignedStep>(input, "step");

    /// <summary>
    /// The extreme member survives the round trip through the accumulator, in both directions — which
    /// the parity above would also catch, but not say. A failure here names the one value at fault
    /// instead of one row of a matrix.
    /// </summary>
    [Theory]
    [InlineData(typeof(ByteScopes))]
    [InlineData(typeof(SByteScopes))]
    [InlineData(typeof(ShortScopes))]
    [InlineData(typeof(UShortScopes))]
    [InlineData(typeof(IntScopes))]
    [InlineData(typeof(UIntScopes))]
    [InlineData(typeof(LongScopes))]
    [InlineData(typeof(ULongScopes))]
    [InlineData(typeof(SignedStep))]
    public void every_declared_value_reads_back_to_itself(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);

        EnumContract contract = EnumContract.For(enumType);

        foreach (object declared in Enum.GetValues(enumType)) {
            string? written = contract.Format(declared);

            Check.WithCustomMessage($"{enumType.Name}.{declared} has no public name.").That(written).IsNotNull();
            Check.WithCustomMessage($"{enumType.Name}: '{written}' could not be read back.")
                 .That(contract.TryParse(written!, out object? read)).IsTrue();
            Check.WithCustomMessage($"{enumType.Name}: '{written}' read back as {read} rather than {declared}.")
                 .That(read).IsEqualTo(declared);
        }
    }

    private async Task Parity<TEnum>(string input, string route) where TEnum : struct, Enum {
        string url = $"/underlying/{route}?value=" + Uri.EscapeDataString(input);
        using HttpResponseMessage response = await api.Client.GetAsync(url, TestContext.Current.CancellationToken);

        TEnum? expected = Oracle<TEnum>.Read(input);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        if (expected is null) {
            Check.WithCustomMessage($"System.Text.Json rejects '{input}' for {typeof(TEnum).Name}, but the query string answered {(int)response.StatusCode} with '{body}'.")
                 .That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

            return;
        }

        Check.WithCustomMessage($"System.Text.Json reads '{input}' as {typeof(TEnum).Name}.{expected}, but the query string answered {(int)response.StatusCode} with '{body}'.")
             .That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using JsonDocument document = JsonDocument.Parse(body);
        Check.That(document.RootElement.GetProperty("value").GetString()).IsEqualTo(expected.Value.ToString());
    }

    private static class Oracle<TEnum> where TEnum : struct, Enum {

        private static readonly JsonSerializerOptions Options = new() {
            Converters = { new JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false) }
        };

        internal static TEnum? Read(string input) {
            try {
                return JsonSerializer.Deserialize<TEnum>(JsonSerializer.Serialize(input), Options);
            } catch (JsonException) {
                return null;
            }
        }

    }

}

/// <summary>One host for the nine enums, started once for the suite above.</summary>
public sealed class UnderlyingTypeApi : IAsyncLifetime {

    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services
               .AddControllers()
               .AddApplicationPart(typeof(UnderlyingTypeController).Assembly)
               .AddEnumMemberNameBinding(options =>
                    options.AddEnum<ByteScopes>().AddEnum<SByteScopes>().AddEnum<ShortScopes>()
                           .AddEnum<UShortScopes>().AddEnum<IntScopes>().AddEnum<UIntScopes>()
                           .AddEnum<LongScopes>().AddEnum<ULongScopes>().AddEnum<SignedStep>());

        _app = builder.Build();
        _app.MapControllers();

        await _app.StartAsync();

        Client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async ValueTask DisposeAsync() {
        Client?.Dispose();
        if (_app is not null) {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

}

[CollectionDefinition(nameof(UnderlyingTypeCollection))]
public sealed class UnderlyingTypeCollection : ICollectionFixture<UnderlyingTypeApi>;
