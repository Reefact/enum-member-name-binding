using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Reefact.AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// Every member on the boundary of a type refuses a null argument it declared non-null.
/// </summary>
public sealed class NullGuardTests {

    [Fact]
    public void adding_the_transformer_to_null_options_is_refused() {
        ArgumentNullException exception = Check.ThatCode(() => EnumMemberNameOpenApiOptionsExtensions.AddEnumMemberNames(null!))
                                               .Throws<ArgumentNullException>().Value;

        Check.That(exception.ParamName).IsEqualTo("options");
    }

    [Fact]
    public void transforming_a_null_schema_is_refused() {
        EnumMemberNameSchemaTransformer transformer = new();

        ArgumentNullException exception = Check.ThatCode(() => { _ = transformer.TransformAsync(null!, null!, CancellationToken.None); })
                                               .Throws<ArgumentNullException>().Value;

        Check.That(exception.ParamName).IsEqualTo("schema");
    }

    [Fact]
    public void transforming_with_a_null_context_is_refused() {
        EnumMemberNameSchemaTransformer transformer = new();
        OpenApiSchema                   schema      = new();

        ArgumentNullException exception = Check.ThatCode(() => { _ = transformer.TransformAsync(schema, null!, CancellationToken.None); })
                                               .Throws<ArgumentNullException>().Value;

        Check.That(exception.ParamName).IsEqualTo("context");
    }

}
