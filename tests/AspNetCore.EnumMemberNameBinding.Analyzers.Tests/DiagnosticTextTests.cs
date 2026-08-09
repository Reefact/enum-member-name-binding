using Microsoft.CodeAnalysis;

namespace AspNetCore.EnumMemberNameBinding.Analyzers.Tests;

/// <summary>
/// Every descriptor resolves real text. A resource key that does not exist renders as the empty
/// string with no exception, so nothing else here would notice a typo.
/// </summary>
public sealed class DiagnosticTextTests {

    public static TheoryData<string> Descriptors {
        get {
            TheoryData<string> data = new();
            foreach (DiagnosticDescriptor descriptor in new EnumContractAnalyzer().SupportedDiagnostics) {
                data.Add(descriptor.Id);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Descriptors))]
    public void a_descriptor_carries_a_title_a_message_and_a_description(string id) {
        DiagnosticDescriptor descriptor = new EnumContractAnalyzer().SupportedDiagnostics.Single(d => d.Id == id);

        Assert.False(string.IsNullOrWhiteSpace(descriptor.Title.ToString()), $"{id} has no title; the resource key is probably misspelt.");
        Assert.False(string.IsNullOrWhiteSpace(descriptor.MessageFormat.ToString()), $"{id} has no message format; the resource key is probably misspelt.");
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()), $"{id} has no description; the resource key is probably misspelt.");
    }

    /// <summary>
    /// The message format is what <c>Diagnostic.Create</c> fills, so a placeholder lost in the move
    /// to resources would silently drop the member name from the reported message.
    /// </summary>
    [Theory]
    [MemberData(nameof(Descriptors))]
    public void a_message_format_still_carries_its_placeholders(string id) {
        DiagnosticDescriptor descriptor = new EnumContractAnalyzer().SupportedDiagnostics.Single(d => d.Id == id);

        Assert.Contains("{0}", descriptor.MessageFormat.ToString(), StringComparison.Ordinal);
    }

}
