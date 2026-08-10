using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// Every member on the boundary of a type refuses a null argument it declared non-null.
/// </summary>
public sealed class NullGuardTests {

    [Fact]
    public void adding_the_transformer_to_null_options_is_refused() {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => EnumMemberNameOpenApiOptionsExtensions.AddEnumMemberNames(null!));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void transforming_a_null_schema_is_refused() {
        EnumMemberNameSchemaTransformer transformer = new();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => { _ = transformer.TransformAsync(null!, null!, CancellationToken.None); });

        Assert.Equal("schema", exception.ParamName);
    }

    [Fact]
    public void transforming_with_a_null_context_is_refused() {
        EnumMemberNameSchemaTransformer transformer = new();
        OpenApiSchema                   schema      = new();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => { _ = transformer.TransformAsync(schema, null!, CancellationToken.None); });

        Assert.Equal("context", exception.ParamName);
    }

}
