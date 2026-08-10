using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using AspNetCore.EnumMemberNameBinding;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

// IDE0130 is right everywhere else and wrong here: the namespace is chosen, for the reason on the
// class below, and it will never match the folder. A pragma rather than [SuppressMessage] because
// the finding is reported on the namespace declaration, which no attribute reaches — measured, not
// assumed: the attribute leaves it standing.
#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Registration of enum member name binding on an <see cref="IMvcBuilder" />.
/// </summary>
/// <remarks>
/// Placed in <c>Microsoft.Extensions.DependencyInjection</c> rather than in this library's own
/// namespace, and deliberately: it is where <see cref="IMvcBuilder" /> itself is declared, so an
/// extension of it belongs with what it extends. The Web SDK imports that namespace implicitly —
/// it is how <c>AddControllers</c> resolves — so
/// <c>builder.Services.AddControllers().AddEnumMemberNameBinding()</c> compiles with no
/// <c>using</c> the caller had to guess. Every registration entry point in these two packages sits
/// here for the same reason.
/// </remarks>
public static class EnumMemberNameBindingMvcBuilderExtensions {

    /// <summary>
    /// Makes route values, query strings, form fields and headers accept the enum member names
    /// declared with <c>[JsonStringEnumMemberName]</c> — the same vocabulary the request body
    /// already accepts.
    /// </summary>
    /// <param name="builder">The MVC builder.</param>
    /// <param name="configure">Optional configuration. By default the entry assembly is scanned.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="EnumContractException">An enum declares an ambiguous or malformed contract.</exception>
    /// <remarks>
    /// Must be called during application start-up. ASP.NET Core caches the model binder it builds
    /// for a given type on first use, so a registration made after the first request has no effect.
    /// <para>
    /// Everything it configures belongs to <paramref name="builder" />'s own container: the set of
    /// registered enums, the model binder provider that reads it, and the two
    /// <c>System.Text.Json</c> option objects. Another application hosted in the same process is
    /// left exactly as it was, whether it starts before this one or after.
    /// </para>
    /// <para>
    /// The converters it installs go ahead of any the application registered itself, so a
    /// <c>JsonStringEnumConverter</c> already in the list does not end up deciding what a contract
    /// enum accepts in the request body. This call may therefore come before or after the
    /// application's own <c>AddJsonOptions</c>; the vocabulary is the same either way. An application
    /// that wants to keep its own converter for a contract enum declines this half with
    /// <see cref="EnumMemberNameBindingOptions.ConfigureJsonSerialization" />, which leaves the
    /// binding in place.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    [RequiresDynamicCode(TrimmingMessages.DynamicCode)]
    public static IMvcBuilder AddEnumMemberNameBinding(this IMvcBuilder builder, Action<EnumMemberNameBindingOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(builder);

        EnumMemberNameBindingOptions options = new();
        configure?.Invoke(options);

        // Resolved and validated first, so a refused contract throws before anything at all has been
        // configured. Nothing here can be undone by the caller, so "the registration did not happen"
        // has to be true rather than nearly true.
        IReadOnlyList<Type> contractEnums = EnumMemberNameBindingRegistry.Register(options);

        Bind(builder.Services, contractEnums);

        // Declining the System.Text.Json half must not decline the binding: a caller who configures
        // their own converters is saying which package writes the JSON, not which one reads a query
        // string. The early return therefore comes after the binder, never before it.
        if (!options.ConfigureJsonSerialization) { return builder; }
        if (contractEnums.Count == 0) { return builder; }

        builder.AddJsonOptions(json => PutInFront(json.JsonSerializerOptions.Converters, contractEnums));

        // MVC and the rest of the stack read two different option objects. Microsoft.AspNetCore.OpenApi
        // and minimal API serialization use Http.Json.JsonOptions, so configuring only the MVC one
        // leaves the generated OpenAPI document describing every contract enum as an integer.
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(
            json => PutInFront(json.SerializerOptions.Converters, contractEnums));

        return builder;
    }

    /// <summary>
    /// Records the contract enums in the application's container and puts the model binder provider
    /// that reads them in front of the one ASP.NET Core uses for enums.
    /// </summary>
    /// <remarks>
    /// The two halves are separate because they accumulate differently. The registrations are one
    /// object per application, so a second call adds to what the first registered; the provider is
    /// one entry in a list, so a second call must not add another. Both are reached through the
    /// container rather than through anything static, which is what keeps one host's registration
    /// out of the next one's.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    private static void Bind(IServiceCollection services, IReadOnlyList<Type> contractEnums) {
        EnumMemberNameBindingRegistrations registrations = Registrations(services);
        registrations.Add(contractEnums);

        services.Configure<MvcOptions>(mvc => {
            if (mvc.ModelBinderProviders.Any(provider => provider is EnumMemberNameModelBinderProvider)) { return; }

            mvc.ModelBinderProviders.Insert(AheadOfTheStockEnumBinder(mvc.ModelBinderProviders), new EnumMemberNameModelBinderProvider(registrations));
        });
    }

    /// <summary>
    /// The application's registration record, created on the first call and shared by every later
    /// one.
    /// </summary>
    /// <remarks>
    /// Read back out of the descriptors rather than kept in a field, because the instance has to be
    /// the one this container will hand to the OpenAPI companion — and a field would be shared by
    /// every container in the process, which is the bug this whole change exists to remove.
    /// </remarks>
    private static EnumMemberNameBindingRegistrations Registrations(IServiceCollection services) {
        foreach (ServiceDescriptor descriptor in services) {
            if (descriptor.ServiceType != typeof(EnumMemberNameBindingRegistrations)) { continue; }
            if (descriptor.ImplementationInstance is EnumMemberNameBindingRegistrations existing) { return existing; }
        }

        EnumMemberNameBindingRegistrations created = new();
        services.AddSingleton(created);

        return created;
    }

    /// <summary>
    /// Where to insert: immediately before the provider that would otherwise claim the parameter.
    /// </summary>
    /// <remarks>
    /// Not at the front, and the difference matters. <c>BodyModelBinderProvider</c> and
    /// <c>HeaderModelBinderProvider</c> sit ahead of the enum one, so a provider inserted at index 0
    /// would take <c>[FromBody]</c> away from <c>System.Text.Json</c> and bind it from the query
    /// string instead.
    /// <para>
    /// The fallbacks are for an application that has rearranged the list. Appending is safe rather
    /// than approximate: every provider after this point claims a collection, a dictionary or a
    /// complex type, and a bare enum is none of those.
    /// </para>
    /// </remarks>
    private static int AheadOfTheStockEnumBinder(IList<IModelBinderProvider> providers) {
        int enums = IndexOf(providers, typeof(EnumTypeModelBinderProvider));
        if (enums >= 0) { return enums; }

        int simple = IndexOf(providers, typeof(SimpleTypeModelBinderProvider));

        return simple >= 0 ? simple : providers.Count;
    }

    private static int IndexOf(IList<IModelBinderProvider> providers, Type providerType) {
        for (int index = 0; index < providers.Count; index++) {
            if (providers[index].GetType() == providerType) { return index; }
        }

        return -1;
    }

    /// <summary>
    /// Puts this package's converters at the head of <paramref name="converters" />, one per contract
    /// enum and in the order they were registered.
    /// </summary>
    /// <remarks>
    /// At the head rather than appended, because <c>System.Text.Json</c> takes the first converter in
    /// the list whose <c>CanConvert</c> answers true. An application that had already registered a
    /// <c>JsonStringEnumConverter</c> of its own would otherwise keep it for the contract enums too,
    /// and the stock converter's default is <c>allowIntegerValues: true</c> — so the request body
    /// would accept <c>1</c> where every other channel answers 400, which is the one divergence this
    /// package exists to remove.
    /// <para>
    /// It also settles the question of order. A converter the application registers after this call is
    /// appended, so it lands behind these either way, and the vocabulary no longer depends on which of
    /// the two registrations ran first. An application that does want its own converter for a contract
    /// enum still has both <c>ConfigureJsonSerialization</c> and an insertion of its own.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    [RequiresDynamicCode(TrimmingMessages.DynamicCode)]
    private static void PutInFront(IList<JsonConverter> converters, IReadOnlyList<Type> contractEnums) {
        for (int index = 0; index < contractEnums.Count; index++) {
            converters.Insert(index, EnumMemberNameBindingRegistry.CreateJsonConverter(contractEnums[index]));
        }
    }

}
