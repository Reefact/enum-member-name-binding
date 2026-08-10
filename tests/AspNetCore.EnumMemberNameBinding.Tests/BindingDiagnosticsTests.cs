using System.Collections.Concurrent;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// The one thing this package's binder does not reproduce: the records ASP.NET Core's own binder
/// writes about itself.
/// </summary>
/// <remarks>
/// <c>SimpleTypeModelBinder</c> is handed an <c>ILoggerFactory</c> and logs its attempt, its result
/// and the absence of a value; this one takes no logger, so a parameter of a contract enum is quiet
/// at Debug where every other parameter is not. Reproducing them is not available rather than
/// declined — they are emitted through <c>MvcCoreLoggerExtensions</c>, which is internal to
/// <c>Microsoft.AspNetCore.Mvc.Core</c> — and a lookalike under this package's own category would
/// read as parity to a log filter aimed at ASP.NET Core's while being none.
/// <para>
/// Held as a test because <c>docs/for-users/limitations.en.md</c> lists it, and that page opens by
/// promising every limitation on it is measured. The second test is the half that keeps the first
/// honest: what is missing is the binder's own two records and nothing else, so a reader knows the
/// surrounding trace still tells them a parameter was bound.
/// </para>
/// </remarks>
public sealed class BindingDiagnosticsTests {

    /// <summary>The category ASP.NET Core's own binders log under.</summary>
    private const string BinderCategory = "Microsoft.AspNetCore.Mvc.ModelBinding.Binders.";

    /// <summary>And the one the layer above them logs under, which this package never replaces.</summary>
    private const string ParameterBinderCategory = "Microsoft.AspNetCore.Mvc.ModelBinding.ParameterBinder";

    [Theory]
    [InlineData("/status/query?value=out_of_stock")]
    [InlineData("/status/query?value=bogus")]
    [InlineData("/status/query")]
    public async Task binding_a_contract_enum_writes_none_of_the_binder_records(string url) {
        await using Host host = await Host.StartAsync();

        IReadOnlyList<string> contract = await host.RecordsFor(url);
        IReadOnlyList<string> plain = await host.RecordsFor("/plain/query?value=High");

        Check.WithCustomMessage("the stock binder logged nothing either, so this test proves nothing.")
             .That(Under(plain, BinderCategory)).Not.IsEmpty();
        Check.WithCustomMessage($"'{url}' wrote binder records after all — the limitation is stale, not the test.")
             .That(Under(contract, BinderCategory)).IsEmpty();
    }

    [Fact]
    public async Task the_trace_around_the_binder_is_written_for_both() {
        await using Host host = await Host.StartAsync();

        IReadOnlyList<string> contract = await host.RecordsFor("/status/query?value=out_of_stock");
        IReadOnlyList<string> plain = await host.RecordsFor("/plain/query?value=High");

        Check.That(Under(plain, ParameterBinderCategory)).Not.IsEmpty();
        Check.WithCustomMessage("a contract enum lost the surrounding trace too, which the documentation says it does not.")
             .That(Under(contract, ParameterBinderCategory)).Not.IsEmpty();
    }

    private static string[] Under(IReadOnlyList<string> records, string category) {
        return [.. records.Where(record => record.StartsWith(category, StringComparison.Ordinal))];
    }

    private sealed class Host : IAsyncDisposable {

        private readonly Recorder _recorder = new();

        private WebApplication _app = null!;

        private HttpClient _client = null!;

        public static async Task<Host> StartAsync() {
            Host host = new();

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(host._recorder);
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            builder.Services
                   .AddControllers()
                   .AddApplicationPart(typeof(BindingController).Assembly)
                   .AddEnumMemberNameBinding(options => options.AddEnum<ProductStatus>());

            host._app = builder.Build();
            host._app.MapControllers();
            await host._app.StartAsync(TestContext.Current.CancellationToken);
            host._client = new HttpClient { BaseAddress = new Uri(host._app.Urls.First()) };

            return host;
        }

        /// <summary>
        /// The records one request wrote. The body is read to completion first: the response is what
        /// says the request is over, and a record written after it would otherwise be counted or not
        /// depending on the run.
        /// </summary>
        public async Task<IReadOnlyList<string>> RecordsFor(string url) {
            _recorder.Records.Clear();

            using HttpResponseMessage response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            return [.. _recorder.Records];
        }

        public async ValueTask DisposeAsync() {
            _client.Dispose();
            await _app.StopAsync(TestContext.Current.CancellationToken);
            await _app.DisposeAsync();
        }

    }

    /// <summary>Keeps every record as "category :: message", which is all these tests ask about.</summary>
    private sealed class Recorder : ILoggerProvider {

        public ConcurrentBag<string> Records { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Records);

        public void Dispose() { }

        private sealed class Sink(string category, ConcurrentBag<string> records) : ILogger {

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) {
                ArgumentNullException.ThrowIfNull(formatter);

                records.Add($"{category} :: [{level}] {formatter(state, error)}");
            }

        }

    }

}
