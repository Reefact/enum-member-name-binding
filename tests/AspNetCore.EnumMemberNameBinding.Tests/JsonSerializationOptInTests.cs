using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// Registration configures <c>System.Text.Json</c> as well as the binder, and both ways of asking it
/// not to are honoured: saying so, and there being nothing to configure.
/// </summary>
/// <remarks>
/// Two option objects, because MVC and the rest of the stack read different ones — configuring only
/// the MVC one leaves the OpenAPI document describing every contract enum as an integer. Both are
/// asserted here for the same reason both are written.
/// </remarks>
public sealed class JsonSerializationOptInTests {

    [Fact]
    public void the_converters_are_installed_by_default() {
        using ServiceProvider provider = Register(options => options.AddEnum<ProductStatus>());

        Assert.NotEmpty(MvcConverters(provider));
        Assert.NotEmpty(HttpConverters(provider));
    }

    [Fact]
    public void nothing_is_configured_when_the_caller_declines_it() {
        using ServiceProvider provider = Register(options => {
            options.AddEnum<ProductStatus>();
            options.ConfigureJsonSerialization = false;
        });

        Assert.Empty(MvcConverters(provider));
        Assert.Empty(HttpConverters(provider));
    }

    /// <summary>
    /// Scanning an assembly that declares no contract is not a failure — it is a registration with
    /// nothing to register, and it must leave serialization exactly as it found it.
    /// </summary>
    [Fact]
    public void nothing_is_configured_when_no_contract_enum_is_found() {
        using ServiceProvider provider = Register(options => options.Assemblies.Add(typeof(EnumContract).Assembly));

        Assert.Empty(MvcConverters(provider));
        Assert.Empty(HttpConverters(provider));
    }

    private static ServiceProvider Register(Action<EnumMemberNameBindingOptions> configure) {
        ServiceCollection services = new();
        services.AddControllers().AddEnumMemberNameBinding(configure);

        return services.BuildServiceProvider();
    }

    private static IList<System.Text.Json.Serialization.JsonConverter> MvcConverters(ServiceProvider provider) {
        return provider.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions.Converters;
    }

    private static IList<System.Text.Json.Serialization.JsonConverter> HttpConverters(ServiceProvider provider) {
        return provider.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions.Converters;
    }

}
