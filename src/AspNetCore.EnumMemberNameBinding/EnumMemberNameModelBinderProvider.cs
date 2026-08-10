using System.Diagnostics.CodeAnalysis;

using DiagnosticCatalog.Trimming;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// Hands an <see cref="EnumMemberNameModelBinder" /> to every parameter whose enum this application
/// registered, and declines everything else.
/// </summary>
/// <remarks>
/// Declining is most of the job. The provider is asked about every bound type in the application,
/// and answering <see langword="null" /> for the ones nobody registered is what leaves an ordinary
/// enum on ASP.NET Core's own binder — the promise that enabling this package changes nothing it was
/// not asked to change. The set it consults belongs to the application's container, which is what
/// makes that promise hold for a neighbouring host in the same process too.
/// </remarks>
internal sealed class EnumMemberNameModelBinderProvider : IModelBinderProvider {

    private readonly EnumMemberNameBindingRegistrations _registrations;

    /// <summary>Creates the provider.</summary>
    /// <remarks>
    /// The trimming requirement is declared here because <see cref="GetBinder" /> resolves a contract
    /// reflectively and, implementing an interface member, cannot carry the annotation itself.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    internal EnumMemberNameModelBinderProvider(EnumMemberNameBindingRegistrations registrations) {
        ArgumentNullException.ThrowIfNull(registrations);

        _registrations = registrations;
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = SuppressionJustification.IL2026.RequirementCarriedByConstructor)]
    public IModelBinder? GetBinder(ModelBinderProviderContext context) {
        ArgumentNullException.ThrowIfNull(context);

        // IsEnum covers the nullable form too, and UnderlyingOrModelType unwraps it — the same pair
        // ASP.NET Core's own EnumTypeModelBinderProvider uses, so `TEnum?` needs nothing of its own.
        if (!context.Metadata.IsEnum) { return null; }

        Type enumType = context.Metadata.UnderlyingOrModelType;
        if (!_registrations.Contains(enumType)) { return null; }

        return new EnumMemberNameModelBinder(EnumContract.For(enumType));
    }

}
