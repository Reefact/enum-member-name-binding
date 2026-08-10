using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// Binds one contract enum from a value provider, using the names declared with
/// <c>[JsonStringEnumMemberName]</c>.
/// </summary>
/// <remarks>
/// A deliberate reimplementation of ASP.NET Core's <c>SimpleTypeModelBinder</c> and the
/// <c>EnumTypeModelBinder</c> that derives from it, with one step replaced: where they call the
/// <see cref="System.ComponentModel.TypeConverter" /> resolved from
/// <see cref="System.ComponentModel.TypeDescriptor" />, this one calls the contract. What is
/// reproduced rather than improved is the binding an application can observe — which value a
/// repeated key yields, when a blank value is null and when it is an error, which of the three
/// <c>ModelBindingMessageProvider</c> sentences a failure earns, and the refusal to bind an
/// undefined value. Those are what an application already expects from every other parameter it
/// binds, and <c>ModelBindingBehaviourTests</c> holds all of them.
///
/// Observable binding, and not the whole of ASP.NET Core's internals: the original is handed an
/// <c>ILoggerFactory</c> and writes the model-binding trace — attempting, found no value, done
/// attempting — where this one writes nothing, so a parameter of a contract enum is silent at Debug
/// where every other parameter is not. That is a limit rather than a decision deferred: those
/// messages are emitted through <c>MvcCoreLoggerExtensions</c>, which is internal to
/// <c>Microsoft.AspNetCore.Mvc.Core</c>. What could be written instead is a lookalike under this
/// package's own category and event ids — which a log filter aimed at ASP.NET Core's would not pick
/// up, so it would read as parity while being none.
///
/// Reimplemented rather than derived because the base class resolves its converter in its
/// constructor, from process-wide state, with no way to supply one — which is exactly the coupling
/// this type exists to remove.
/// </remarks>
internal sealed class EnumMemberNameModelBinder : IModelBinder {

    private readonly EnumContract _contract;

    internal EnumMemberNameModelBinder(EnumContract contract) {
        ArgumentNullException.ThrowIfNull(contract);

        _contract = contract;
    }

    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext) {
        ArgumentNullException.ThrowIfNull(bindingContext);

        ValueProviderResult value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None) { return Task.CompletedTask; }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);

        // A repeated key is one combination for a [Flags] enum — StringValues joins with a comma,
        // which is the separator anyway — and the first value alone for any other. The rule is read
        // off the metadata, so it stays ASP.NET Core's rather than becoming this package's.
        string? text = bindingContext.ModelMetadata.IsFlagsEnum ? value.Values.ToString() : value.FirstValue;

        if (string.IsNullOrWhiteSpace(text)) { return Blank(bindingContext, value); }
        if (_contract.TryParse(text, out object? model)) { return Parsed(bindingContext, value, model); }

        // The exception carries the sentence naming the allowed values, and ASP.NET Core then writes
        // its own into ModelState. Both are deliberate: what a client reads is the platform's
        // wording for every parameter of the application, and what a log reads is this one.
        bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, new FormatException(NotAValidValue(text)), bindingContext.ModelMetadata);

        return Task.CompletedTask;
    }

    private string NotAValidValue(string text) {
        return $"'{text}' is not a valid value for {_contract.EnumType.Name}. Allowed values: {_contract.AllowedValues}.";
    }

    /// <summary>
    /// A value that is present but blank. Nothing is parsed, so this is the null case: allowed on a
    /// nullable parameter, and an error on any other.
    /// </summary>
    private static Task Blank(ModelBindingContext bindingContext, ValueProviderResult value) {
        if (bindingContext.ModelMetadata.IsReferenceOrNullableType) {
            bindingContext.Result = ModelBindingResult.Success(null);

            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                                                   bindingContext.ModelMetadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(value.ToString()));

        return Task.CompletedTask;
    }

    private static Task Parsed(ModelBindingContext bindingContext, ValueProviderResult value, object model) {
        if (IsDefined(bindingContext, model)) {
            bindingContext.Result = ModelBindingResult.Success(model);

            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                                                   bindingContext.ModelMetadata.ModelBindingMessageProvider.ValueIsInvalidAccessor(value.ToString()));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether ASP.NET Core will let this value through — the check that keeps a combination naming
    /// no member out of a non-<c>[Flags]</c> parameter.
    /// </summary>
    /// <remarks>
    /// Reproduced from <c>EnumTypeModelBinder</c>, and this is the one input where a channel and the
    /// request body disagree: <c>System.Text.Json</c> reads <c>"out_of_stock,discontinued"</c> as
    /// <c>1 | 2</c> and hands back a value no member declares, and ASP.NET Core will not bind it.
    /// Reproducing that is the decision rather than an omission — an enum this package never touches
    /// is refused the same way, so letting it through would make a contract enum the more permissive
    /// of the two, and the promise runs the other way. It is written down in
    /// <c>docs/for-users/limitations.en.md</c>.
    /// <para>
    /// A <c>[Flags]</c> parameter is answered without asking, where the original runs a test of its
    /// own — <see cref="Enum.IsDefined(Type, object)" /> does not work on a combination, so it
    /// compares the value's text against its underlying number to find one that decomposes into no
    /// members. Nothing reaching here can be that value: it was built by OR-ing members the contract
    /// declares, so it decomposes into them by construction. Running the test anyway would be a
    /// branch that cannot be taken, dressed as a decision.
    /// </para>
    /// </remarks>
    private static bool IsDefined(ModelBindingContext bindingContext, object model) {
        if (bindingContext.ModelMetadata.IsFlagsEnum) { return true; }

        return Enum.IsDefined(bindingContext.ModelMetadata.UnderlyingOrModelType, model);
    }

}
