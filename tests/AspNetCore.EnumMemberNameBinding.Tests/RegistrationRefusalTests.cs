using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// A registration that is refused configures nothing at all, and a registration repeated configures
/// one thing once.
/// </summary>
/// <remarks>
/// Both are asserted through the container rather than through the return value of
/// <c>Register</c>, because the container is what the application will actually run on. The
/// refusals are decided in two different places — whether a named type declares a contract at all is
/// settled before discovery yields anything, whether its contract is complete is settled per type —
/// and a caller reading the exception cannot tell which one answered, so both have to leave the
/// same nothing behind.
/// </remarks>
public sealed class RegistrationRefusalTests {

    public enum ValidButRefusedAlongside {

        [JsonStringEnumMemberName("alpha")] Alpha

    }

    public enum DeclaresNoContract {

        Beta

    }

    public enum ValidBesideAPartialOne {

        [JsonStringEnumMemberName("gamma")] Gamma

    }

    public enum PartiallyAnnotatedHere {

        [JsonStringEnumMemberName("delta")] Delta,
        Epsilon

    }

    public enum Repeated {

        [JsonStringEnumMemberName("first")]  First,
        [JsonStringEnumMemberName("second")] Second

    }

    /// <summary>The good enum is named first on purpose: it is the one that would have been installed.</summary>
    [Fact]
    public void a_refused_registration_configures_nothing_at_all() {
        ServiceCollection services = new();
        IMvcBuilder       mvc      = services.AddControllers();

        Check.ThatCode(() => mvc.AddEnumMemberNameBinding(options => {
                 options.AddEnum<ValidButRefusedAlongside>();
                 options.AddEnum<DeclaresNoContract>();
             })).Throws<EnumContractException>();

        AssertNothingWasConfigured(services);
    }

    [Fact]
    public void a_partial_contract_refused_late_configures_nothing_either() {
        ServiceCollection services = new();
        IMvcBuilder       mvc      = services.AddControllers();

        Check.ThatCode(() => mvc.AddEnumMemberNameBinding(options => {
                 options.AddEnum<ValidBesideAPartialOne>();
                 options.AddEnum<PartiallyAnnotatedHere>();
             })).Throws<EnumContractException>();

        AssertNothingWasConfigured(services);
    }

    /// <summary>
    /// The registrations accumulate and the provider does not. Both halves are asserted, because
    /// getting one right and the other wrong is silent either way: a second provider would shadow
    /// nothing visibly, and a second record would lose the first call's enums.
    /// </summary>
    [Fact]
    public void registering_repeatedly_leaves_one_provider_covering_every_call() {
        ServiceCollection services = new();
        IMvcBuilder       mvc      = services.AddControllers();

        mvc.AddEnumMemberNameBinding(options => options.AddEnum<Repeated>());
        mvc.AddEnumMemberNameBinding(options => options.AddEnum<Repeated>());
        mvc.AddEnumMemberNameBinding(options => options.AddEnum<ValidButRefusedAlongside>());

        using ServiceProvider provider = services.BuildServiceProvider();

        Check.That(OurProviders(provider)).HasSize(1);

        EnumMemberNameBindingRegistrations registrations = provider.GetRequiredService<EnumMemberNameBindingRegistrations>();

        Check.That(registrations.Contains(typeof(Repeated))).IsTrue();
        Check.That(registrations.Contains(typeof(ValidButRefusedAlongside))).IsTrue();
        Check.That(registrations.Contains(typeof(PlainPriority))).IsFalse();
    }

    /// <summary>
    /// Where the provider lands, in the three lists it can meet. Ahead of the enum provider normally;
    /// ahead of the simple-type one if an application removed that; at the end if it removed both.
    /// </summary>
    /// <remarks>
    /// The position is not decoration. Ahead of the stock enum binder is what makes the contract win;
    /// behind <c>BodyModelBinderProvider</c> and <c>HeaderModelBinderProvider</c> is what leaves
    /// <c>[FromBody]</c> to <c>System.Text.Json</c> — which is why inserting at index 0 would be
    /// wrong and why the fallbacks land where they do. Appending is safe rather than approximate:
    /// every provider past that point claims a collection, a dictionary or a complex type, and a bare
    /// enum is none of those.
    /// </remarks>
    [Theory]
    [InlineData(0, typeof(EnumTypeModelBinderProvider))]
    [InlineData(1, typeof(SimpleTypeModelBinderProvider))]
    [InlineData(2, null)]
    public void the_provider_lands_ahead_of_whichever_stock_binder_would_have_claimed_the_parameter(int removed, Type? expectedSuccessor) {
        ServiceCollection services = new();
        services.AddControllers(mvc => RemoveStockBinders(mvc.ModelBinderProviders, removed))
                .AddEnumMemberNameBinding(options => options.AddEnum<Repeated>());

        using ServiceProvider provider = services.BuildServiceProvider();
        IList<IModelBinderProvider> providers = provider.GetRequiredService<IOptions<MvcOptions>>().Value.ModelBinderProviders;

        int at = providers.IndexOf(providers.Single(binder => binder is EnumMemberNameModelBinderProvider));

        Check.WithCustomMessage("at the front it would take [FromBody] away from System.Text.Json.").That(at).IsNotEqualTo(0);
        Check.That(at + 1 < providers.Count ? providers[at + 1].GetType() : null).IsEqualTo(expectedSuccessor);
    }

    /// <summary>
    /// The refusal that does not come from this package at all: a call made after
    /// <c>WebApplicationBuilder.Build</c>, when the service collection has gone read-only. It must
    /// leave the same nothing behind as the ones decided here.
    /// </summary>
    /// <remarks>
    /// Out of the documented start-up window, and that is what makes it worth pinning rather than
    /// worth allowing. The record is live — the model binder provider installed by the first call
    /// reads it on every request — so filling it before the steps that can throw left a running
    /// application binding an enum by names it had just been told were not registered, and
    /// serializing it as a number, because no converter went with it. The call reported total
    /// failure while changing the application: precisely the divergence this package exists to
    /// remove, produced by the package.
    /// <para>
    /// The builder is captured before <c>Build</c> so the second call reaches
    /// <c>AddEnumMemberNameBinding</c> at all — going through <c>AddControllers</c> again would throw
    /// before it, proving nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void a_registration_that_throws_after_the_container_is_built_records_nothing() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        IMvcBuilder mvc = builder.Services.AddControllers();
        mvc.AddEnumMemberNameBinding(options => options.AddEnum<Repeated>());

        using WebApplication app = builder.Build();

        Check.WithCustomMessage("the service collection is read-only once the container is built, so this call cannot succeed.")
             .ThatCode(() => mvc.AddEnumMemberNameBinding(options => options.AddEnum<ValidBesideAPartialOne>()))
             .Throws<InvalidOperationException>();

        EnumMemberNameBindingRegistrations registrations = app.Services.GetRequiredService<EnumMemberNameBindingRegistrations>();

        Check.WithCustomMessage("the call threw, so the enum it named must not be bound by the running application.")
             .That(registrations.Contains(typeof(ValidBesideAPartialOne))).IsFalse();
        Check.WithCustomMessage("and the registration that did succeed must survive it.")
             .That(registrations.Contains(typeof(Repeated))).IsTrue();
    }

    /// <summary>The two stock providers that claim an enum, dropped in the order they are consulted.</summary>
    private static void RemoveStockBinders(IList<IModelBinderProvider> providers, int count) {
        Type[] stock = [typeof(EnumTypeModelBinderProvider), typeof(SimpleTypeModelBinderProvider)];

        foreach (Type type in stock.Take(count)) {
            providers.Remove(providers.Single(provider => provider.GetType() == type));
        }
    }

    private static void AssertNothingWasConfigured(IServiceCollection services) {
        using ServiceProvider provider = services.BuildServiceProvider();

        Check.That(OurProviders(provider)).IsEmpty();
        Check.That(provider.GetService<EnumMemberNameBindingRegistrations>()).IsNull();
    }

    private static IReadOnlyList<IModelBinderProvider> OurProviders(ServiceProvider provider) {
        return [.. provider.GetRequiredService<IOptions<MvcOptions>>().Value
                           .ModelBinderProviders.Where(binder => binder is EnumMemberNameModelBinderProvider)];
    }

}
