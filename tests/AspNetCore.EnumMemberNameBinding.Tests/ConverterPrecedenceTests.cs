using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using DiagnosticCatalog.NetAnalyzers;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>A contract enum, in a host that also installs a converter of its own.</summary>
public enum Shipment {

    [JsonStringEnumMemberName("in_transit")] InTransit,
    [JsonStringEnumMemberName("delivered")]  Delivered

}

/// <summary>No contract at all: the enum the application's own converter is there for.</summary>
public enum Carrier {

    Road,
    Air

}

// Top level on purpose: MVC's controller discovery requires Type.IsPublic, which is false for a
// nested type, so a nested controller is silently never routed.
[ApiController]
public sealed class ShipmentController : ControllerBase {

    [HttpGet("/shipment/query")]
    public IActionResult FromQuery([FromQuery] Shipment value) => Ok(new { value = value.ToString() });

    [HttpPost("/shipment/body")]
    [SuppressMessage(NetAnalyzersRule.CA1062.Category, NetAnalyzersRule.CA1062.Id, Justification = SuppressionJustification.CA1062.ArgumentSuppliedByTheFramework)]
    public IActionResult FromBody([FromBody] ShipmentPayload payload) => Ok(new { value = payload.Value.ToString() });

    [HttpGet("/shipment/serialized")]
    public IActionResult Serialized() => Ok(new { value = Shipment.InTransit });

    [HttpGet("/carrier/query")]
    public IActionResult CarrierFromQuery([FromQuery] Carrier value) => Ok(new { value = value.ToString() });

    [HttpGet("/carrier/serialized")]
    public IActionResult CarrierSerialized() => Ok(new { value = Carrier.Air });

}

public sealed class ShipmentPayload {

    public Shipment Value { get; set; }

}

/// <summary>
/// An application that registers a <c>JsonStringEnumConverter</c> of its own must not end up
/// deciding the vocabulary of a contract enum, whichever of the two registrations came first.
/// </summary>
/// <remarks>
/// <c>System.Text.Json</c> takes the first converter in the list whose <c>CanConvert</c> answers
/// true, and the stock converter answers true for every enum. Appended, this package's converters
/// sat behind one the application had already installed — and the stock converter's default is
/// <c>allowIntegerValues: true</c>, so the request body accepted <c>1</c> while the query string
/// answered 400. That is the divergence the README rules out in as many words: "An unknown or
/// numeric value is a 400, exactly as the body would refuse it."
/// <para>
/// Both orders are asserted because appending made the outcome depend on which call ran first, and
/// "the same vocabulary everywhere" cannot be a property of the order the caller happened to write.
/// </para>
/// </remarks>
public sealed class ConverterPrecedenceTests {

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task the_body_refuses_a_number_whichever_registration_came_first(bool callerFirst) {
        await using Host host = await Host.StartAsync(callerFirst);

        Check.WithCustomMessage("the request body accepted a numeric value.")
             .That(await BodyStatus(host, "1")).IsEqualTo(HttpStatusCode.BadRequest);
        Check.WithCustomMessage("the request body accepted a value naming no member.")
             .That(await BodyStatus(host, "999")).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task the_body_and_the_query_string_answer_the_same_thing(bool callerFirst) {
        await using Host host = await Host.StartAsync(callerFirst);

        foreach (string accepted in new[] { "in_transit", "delivered" }) {
            Check.WithCustomMessage($"the query string refused '{accepted}'.")
                 .That(await QueryStatus(host, accepted)).IsEqualTo(HttpStatusCode.OK);
            Check.WithCustomMessage($"the request body refused '{accepted}'.")
                 .That(await BodyStatus(host, $"\"{accepted}\"")).IsEqualTo(HttpStatusCode.OK);
        }

        foreach (string refused in new[] { "InTransit", "1", "bogus" }) {
            Check.WithCustomMessage($"the query string accepted '{refused}'.")
                 .That(await QueryStatus(host, refused)).IsEqualTo(HttpStatusCode.BadRequest);
            Check.WithCustomMessage($"the request body accepted '{refused}'.")
                 .That(await BodyStatus(host, $"\"{refused}\"")).IsEqualTo(HttpStatusCode.BadRequest);
        }
    }

    /// <summary>
    /// The same list, read the other way: what this package installs must not take an enum it was
    /// never given away from the converter the application installed for it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task an_enum_this_package_never_registered_stays_with_the_application_converter(bool callerFirst) {
        await using Host host = await Host.StartAsync(callerFirst);

        Check.That(await Text(host, "/carrier/serialized")).IsEqualTo("""{"value":"Air"}""");
        Check.That(await QueryStatus(host, "Air", "/carrier/query")).IsEqualTo(HttpStatusCode.OK);
    }

    /// <summary>
    /// Both option objects, because they are configured by two separate calls and only one of them
    /// is MVC's. A minimal API endpoint and the OpenAPI document read <c>Http.Json.JsonOptions</c>.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task both_option_objects_write_the_declared_name(bool callerFirst) {
        await using Host host = await Host.StartAsync(callerFirst);

        Check.WithCustomMessage("the MVC options wrote something other than the declared name.")
             .That(await Text(host, "/shipment/serialized")).IsEqualTo("""{"value":"in_transit"}""");
        Check.WithCustomMessage("the Http.Json options wrote something other than the declared name.")
             .That(await Text(host, "/minimal/shipment")).IsEqualTo("""{"value":"in_transit"}""");
    }

    private static async Task<HttpStatusCode> QueryStatus(Host host, string value, string path = "/shipment/query") {
        using HttpResponseMessage response = await host.Client.GetAsync(path + "?value=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> BodyStatus(Host host, string json) {
        using StringContent content = new($$"""{"value":{{json}}}""", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await host.Client.PostAsync("/shipment/body", content, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private static async Task<string> Text(Host host, string url) {
        using HttpResponseMessage response = await host.Client.GetAsync(url, TestContext.Current.CancellationToken);

        Check.WithCustomMessage($"{url} answered {(int)response.StatusCode}.").That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private sealed class Host : IAsyncDisposable {

        private WebApplication _app = null!;

        public HttpClient Client { get; private set; } = null!;

        /// <summary>
        /// A host that installs the stock converter for every enum, and this package for one of them.
        /// </summary>
        /// <param name="callerFirst">
        /// Whether the application's own <c>AddJsonOptions</c> runs before this package's
        /// registration. Both option objects are configured in registration order, so this is exactly
        /// the knob that used to decide which converter won.
        /// </param>
        public static async Task<Host> StartAsync(bool callerFirst) {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            IMvcBuilder mvc = builder.Services.AddControllers().AddApplicationPart(typeof(ShipmentController).Assembly);

            if (callerFirst) { Caller(mvc); }
            mvc.AddEnumMemberNameBinding(options => options.AddEnum<Shipment>());
            if (!callerFirst) { Caller(mvc); }

            Host host = new() { _app = builder.Build() };
            host._app.MapControllers();
            host._app.MapGet("/minimal/shipment", () => new { value = Shipment.InTransit });
            await host._app.StartAsync();
            host.Client = new HttpClient { BaseAddress = new Uri(host._app.Urls.First()) };

            return host;
        }

        /// <summary>
        /// What the application asked for: string enums everywhere, on both option objects, exactly as
        /// an application that wanted them before it had ever heard of this package would write it.
        /// </summary>
        private static void Caller(IMvcBuilder mvc) {
            mvc.AddJsonOptions(json => json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            mvc.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(
                json => json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        }

        public async ValueTask DisposeAsync() {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

    }

}
