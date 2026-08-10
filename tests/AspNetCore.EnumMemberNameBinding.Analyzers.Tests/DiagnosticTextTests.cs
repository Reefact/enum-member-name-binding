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

        Check.WithCustomMessage($"{id} has no title; the resource key is probably misspelt.")
             .That(string.IsNullOrWhiteSpace(descriptor.Title.ToString())).IsFalse();
        Check.WithCustomMessage($"{id} has no message format; the resource key is probably misspelt.")
             .That(string.IsNullOrWhiteSpace(descriptor.MessageFormat.ToString())).IsFalse();
        Check.WithCustomMessage($"{id} has no description; the resource key is probably misspelt.")
             .That(string.IsNullOrWhiteSpace(descriptor.Description.ToString())).IsFalse();
    }

    /// <summary>
    /// The message format is what <c>Diagnostic.Create</c> fills, so a placeholder lost in the move
    /// to resources would silently drop the member name from the reported message.
    /// </summary>
    [Theory]
    [MemberData(nameof(Descriptors))]
    public void a_message_format_still_carries_its_placeholders(string id) {
        DiagnosticDescriptor descriptor = new EnumContractAnalyzer().SupportedDiagnostics.Single(d => d.Id == id);

        Check.That(descriptor.MessageFormat.ToString()).Contains("{0}");
    }

}
