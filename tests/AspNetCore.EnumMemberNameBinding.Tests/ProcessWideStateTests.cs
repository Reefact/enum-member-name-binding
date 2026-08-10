using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCore.EnumMemberNameBinding.Tests;

public enum Repeated {

    [JsonStringEnumMemberName("first")]  First,
    [JsonStringEnumMemberName("second")] Second

}

// Top level on purpose: Type.IsPublic is false for a nested type, and MVC's controller discovery
// requires IsPublic, so a nested controller is silently never routed.
[ApiController]
public sealed class RepeatedController : ControllerBase {

    [HttpGet("/repeated")]
    public IActionResult Get([FromQuery] Repeated value) => Ok(new { value = value.ToString() });

}

/// <summary>
/// <c>TypeDescriptor.AddAttributes</c> mutates state shared by the whole process, so registering
/// twice, or hosting several applications side by side, must not degrade anything.
/// </summary>
public sealed class ProcessWideStateTests {

    public enum ValidButRefusedAlongside {

        [JsonStringEnumMemberName("alpha")] Alpha

    }

    public enum DeclaresNoContract {

        Beta

    }

    /// <summary>
    /// A registration that names one good enum and one bad one installs neither.
    /// </summary>
    /// <remarks>
    /// This matters more here than it would elsewhere, and that is why the test lives in this file:
    /// <c>TypeDescriptor.AddAttributes</c> cannot be undone. A registration that installed the
    /// converter for the members it had already reached before refusing the rest would leave the
    /// process permanently in a state the caller never asked for and cannot roll back — a start-up
    /// failure that still changed how the application behaves.
    ///
    /// The good enum is named first on purpose. It is the one that would have been installed.
    /// </remarks>
    [Fact]
    public void a_refused_registration_installs_nothing_at_all() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<ValidButRefusedAlongside>();
        options.AddEnum<DeclaresNoContract>();

        Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options)).Throws<EnumContractException>();

        Check.That(TypeDescriptor.GetConverter(typeof(ValidButRefusedAlongside))).IsNotInstanceOf<EnumMemberNameConverter>();
    }

    public enum ValidBesideAPartialOne {

        [JsonStringEnumMemberName("gamma")] Gamma

    }

    public enum PartiallyAnnotatedHere {

        [JsonStringEnumMemberName("delta")] Delta,
        Epsilon

    }

    /// <summary>
    /// A partial contract is refused on the same terms as any other: nothing is installed.
    /// </summary>
    /// <remarks>
    /// The sibling of the test above, and the reason it exists separately: the two refusals are
    /// decided in different places. Whether a named type is an enum at all, and whether it declares
    /// a contract, are settled before discovery yields anything; whether its contract is complete is
    /// settled per type, and used to be settled inside the loop that installs. A caller cannot see
    /// which check refused them, so both must leave the process alone.
    /// </remarks>
    [Fact]
    public void a_partial_contract_refused_late_installs_nothing_either() {
        EnumMemberNameBindingOptions options = new();
        options.AddEnum<ValidBesideAPartialOne>();
        options.AddEnum<PartiallyAnnotatedHere>();

        Check.ThatCode(() => EnumMemberNameBindingRegistry.Register(options)).Throws<EnumContractException>();

        Check.That(TypeDescriptor.GetConverter(typeof(ValidBesideAPartialOne))).IsNotInstanceOf<EnumMemberNameConverter>();
    }

    [Fact]
    public void registering_the_same_type_repeatedly_leaves_one_converter_in_place() {
        for (int attempt = 0; attempt < 5; attempt++) {
            EnumMemberNameBindingOptions options = new();
            options.AddEnum<Repeated>();

            Check.That(EnumMemberNameBindingRegistry.Register(options)).Contains(typeof(Repeated));
        }

        TypeConverter converter = TypeDescriptor.GetConverter(typeof(Repeated));

        Check.That(converter).IsInstanceOf<EnumMemberNameConverter>();
        Check.That(converter.ConvertFromString("second")).IsEqualTo(Repeated.Second);
    }

    [Fact]
    public async Task two_hosts_in_the_same_process_both_bind_correctly() {
        await using Host first = await Host.StartAsync();
        await using Host second = await Host.StartAsync();

        foreach (Host host in new[] { first, second }) {
            using HttpResponseMessage accepted = await host.Client.GetAsync("/repeated?value=second", TestContext.Current.CancellationToken);
            using HttpResponseMessage refused = await host.Client.GetAsync("/repeated?value=Second", TestContext.Current.CancellationToken);

            Check.That(accepted.StatusCode).IsEqualTo(HttpStatusCode.OK);
            using JsonDocument document = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Check.That(document.RootElement.GetProperty("value").GetString()).IsEqualTo(nameof(Repeated.Second));

            Check.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task a_host_started_after_another_has_stopped_still_binds_correctly() {
        await using (Host first = await Host.StartAsync()) {
            using HttpResponseMessage response = await first.Client.GetAsync("/repeated?value=first", TestContext.Current.CancellationToken);
            Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        await using Host second = await Host.StartAsync();
        using HttpResponseMessage again = await second.Client.GetAsync("/repeated?value=first", TestContext.Current.CancellationToken);

        Check.That(again.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private sealed class Host : IAsyncDisposable {

        private WebApplication _app = null!;

        public HttpClient Client { get; private set; } = null!;

        public static async Task<Host> StartAsync() {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services
                   .AddControllers()
                   .AddApplicationPart(typeof(RepeatedController).Assembly)
                   .AddEnumMemberNameBinding(options => options.AddEnum<Repeated>());

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
