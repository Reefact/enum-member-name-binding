using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// A contract enum registered by one host and deliberately not by another. The declared names differ
/// from the C# names by more than casing, which is what lets a request tell the two apart — the
/// stock binder matches a C# name case-insensitively, so an enum whose declared name is its own name
/// in another casing would answer identically either way and prove nothing.
/// </summary>
public enum Isolated {

    [JsonStringEnumMemberName("out_of_stock")] OutOfStock,
    [JsonStringEnumMemberName("available")]    Available

}

// Top level on purpose: MVC's controller discovery requires Type.IsPublic, which is false for a
// nested type, so a nested controller is silently never routed.
[ApiController]
public sealed class IsolatedController : ControllerBase {

    [HttpGet("/isolated")]
    public IActionResult Get([FromQuery] Isolated value) => Ok(new { value = value.ToString() });

    [HttpGet("/isolated/serialized")]
    public IActionResult Serialized() => Ok(new { value = Isolated.OutOfStock });

}

/// <summary>
/// Registration belongs to the host that asked for it. Several applications can share a process — a
/// test suite, a tool hosting more than one <c>WebApplication</c>, an embedded host — and one of them
/// enabling this package must leave the others exactly as they were.
/// </summary>
/// <remarks>
/// This is the suite that decided the binding could not stay on <c>TypeDescriptor.AddAttributes</c>.
/// That call mutates state belonging to the process, so a host that never referenced this package
/// still resolved the contract converter and bound by the declared names — while its own
/// <c>System.Text.Json</c> options, which live in its container, kept writing numbers. The two halves
/// of its contract disagreed, and there was nowhere to document it that its author would ever read.
/// </remarks>
public sealed class HostIsolationTests {

    /// <summary>
    /// The opted-in host is asserted first, and that order is the test: it proves the registration
    /// really happened, so the opted-out host answering stock cannot be a registration that silently
    /// did nothing.
    /// </summary>
    [Fact]
    public async Task a_host_that_never_registered_anything_keeps_the_stock_behaviour() {
        await using Host optedIn = await Host.StartAsync(register: true);
        await AssertBindsByDeclaredName(optedIn);

        await using Host optedOut = await Host.StartAsync(register: false);
        await AssertBindsByCsharpName(optedOut);
    }

    /// <summary>
    /// The same, with the opted-out host built first and asked last. Ordering is what made the leak
    /// intermittent: a host whose binder was already cached kept the stock behaviour, and one that
    /// had not served a request yet did not.
    /// </summary>
    [Fact]
    public async Task the_order_the_two_hosts_start_in_makes_no_difference() {
        await using Host optedOut = await Host.StartAsync(register: false);
        await using Host optedIn = await Host.StartAsync(register: true);

        await AssertBindsByDeclaredName(optedIn);
        await AssertBindsByCsharpName(optedOut);
    }

    /// <summary>
    /// Serialization was never leaked — it is configured through the host's own container — so the
    /// two halves of the opted-out host's contract have to agree with each other: C# names in, and
    /// the numeric wire format out, which is what an application that never asked for anything gets.
    /// </summary>
    [Fact]
    public async Task the_json_format_of_an_opted_out_host_agrees_with_how_it_binds() {
        await using Host optedIn = await Host.StartAsync(register: true);
        await using Host optedOut = await Host.StartAsync(register: false);

        Check.That(await Text(optedIn, "/isolated/serialized")).IsEqualTo("""{"value":"out_of_stock"}""");
        Check.That(await Text(optedOut, "/isolated/serialized")).IsEqualTo("""{"value":0}""");
    }

    /// <summary>
    /// Two hosts that both asked for it, which is the ordinary case and the one that used to be the
    /// only one covered: registering twice must not degrade either of them.
    /// </summary>
    [Fact]
    public async Task two_hosts_that_both_registered_the_same_enum_both_bind_by_it() {
        await using Host first = await Host.StartAsync(register: true);
        await using Host second = await Host.StartAsync(register: true);

        await AssertBindsByDeclaredName(first);
        await AssertBindsByDeclaredName(second);
    }

    [Fact]
    public async Task a_host_started_after_another_has_stopped_still_binds_correctly() {
        await using (Host first = await Host.StartAsync(register: true)) {
            await AssertBindsByDeclaredName(first);
        }

        await using Host second = await Host.StartAsync(register: true);
        await AssertBindsByDeclaredName(second);
    }

    private static async Task AssertBindsByDeclaredName(Host host) {
        Check.That(await Bind(host, "out_of_stock")).IsEqualTo(nameof(Isolated.OutOfStock));
        Check.That(await Status(host, "OutOfStock")).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static async Task AssertBindsByCsharpName(Host host) {
        Check.WithCustomMessage("a host that never called AddEnumMemberNameBinding bound by a declared name.")
             .That(await Status(host, "out_of_stock")).IsEqualTo(HttpStatusCode.BadRequest);
        Check.That(await Bind(host, "OutOfStock")).IsEqualTo(nameof(Isolated.OutOfStock));
        Check.That(await Bind(host, "outofstock")).IsEqualTo(nameof(Isolated.OutOfStock));
    }

    private static async Task<HttpStatusCode> Status(Host host, string value) {
        using HttpResponseMessage response = await host.Client.GetAsync("/isolated?value=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private static async Task<string> Bind(Host host, string value) {
        using JsonDocument document = JsonDocument.Parse(await Text(host, "/isolated?value=" + Uri.EscapeDataString(value)));

        return document.RootElement.GetProperty("value").GetString()!;
    }

    private static async Task<string> Text(Host host, string url) {
        using HttpResponseMessage response = await host.Client.GetAsync(url, TestContext.Current.CancellationToken);

        Check.WithCustomMessage($"{url} answered {(int)response.StatusCode}.").That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private sealed class Host : IAsyncDisposable {

        private WebApplication _app = null!;

        public HttpClient Client { get; private set; } = null!;

        public static async Task<Host> StartAsync(bool register) {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            IMvcBuilder mvc = builder.Services.AddControllers().AddApplicationPart(typeof(IsolatedController).Assembly);
            if (register) { mvc.AddEnumMemberNameBinding(options => options.AddEnum<Isolated>()); }

            Host host = new() { _app = builder.Build() };
            host._app.MapControllers();
            await host._app.StartAsync();
            host.Client = new HttpClient { BaseAddress = new Uri(host._app.Urls.First()) };

            return host;
        }

        public async ValueTask DisposeAsync() {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

    }

}
