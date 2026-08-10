using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
