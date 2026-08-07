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

    [Fact]
    public void registering_the_same_type_repeatedly_leaves_one_converter_in_place() {
        for (int attempt = 0; attempt < 5; attempt++) {
            EnumMemberNameBindingOptions options = new();
            options.AddEnum<Repeated>();

            Assert.Contains(typeof(Repeated), EnumMemberNameBindingRegistry.Register(options));
        }

        TypeConverter converter = TypeDescriptor.GetConverter(typeof(Repeated));

        Assert.IsType<EnumMemberNameConverter>(converter);
        Assert.Equal(Repeated.Second, converter.ConvertFromString("second"));
    }

    [Fact]
    public async Task two_hosts_in_the_same_process_both_bind_correctly() {
        await using Host first = await Host.StartAsync();
        await using Host second = await Host.StartAsync();

        foreach (Host host in new[] { first, second }) {
            using HttpResponseMessage accepted = await host.Client.GetAsync("/repeated?value=second", TestContext.Current.CancellationToken);
            using HttpResponseMessage refused = await host.Client.GetAsync("/repeated?value=Second", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            using JsonDocument document = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(nameof(Repeated.Second), document.RootElement.GetProperty("value").GetString());

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }
    }

    [Fact]
    public async Task a_host_started_after_another_has_stopped_still_binds_correctly() {
        await using (Host first = await Host.StartAsync()) {
            using HttpResponseMessage response = await first.Client.GetAsync("/repeated?value=first", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using Host second = await Host.StartAsync();
        using HttpResponseMessage again = await second.Client.GetAsync("/repeated?value=first", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
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
