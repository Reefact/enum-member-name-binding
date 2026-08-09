using Microsoft.CodeAnalysis.Diagnostics;

namespace AspNetCore.EnumMemberNameBinding.Analyzers.Tests;

/// <summary>
/// The analyzer's one boundary member refuses a null argument it declared non-null.
/// </summary>
public sealed class NullGuardTests {

    [Fact]
    public void initializing_with_a_null_context_is_refused_rather_than_ignored() {
        EnumContractAnalyzer analyzer = new();

        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(() => analyzer.Initialize(null!));

        Assert.Equal("context", exception.ParamName);
    }

}
