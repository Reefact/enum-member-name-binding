using System.Text.Json.Serialization;

using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// Named by nothing and reached by no endpoint: only a scan can find it, which is the whole point of
/// declaring it.
/// </summary>
public enum Delivery {

    [JsonStringEnumMemberName("standard")] Standard,
    [JsonStringEnumMemberName("express")]  Express

}

/// <summary>
/// The default, and the form the documentation opens with: configure nothing, and the entry assembly
/// is scanned.
/// </summary>
/// <remarks>
/// Exercised from this suite rather than from the core package's own, and the entry assembly is the
/// reason — under the test runner it is the test assembly itself. This one declares only valid
/// contracts. The core suite deliberately declares malformed ones, so a scan there would refuse,
/// correctly, before proving anything about the scan.
/// </remarks>
public sealed class EntryAssemblyScanTests {

    /// <summary>
    /// Read back from the application's own registration record, which is also what the OpenAPI
    /// companion consults — so this asserts the scan against the very thing that will decide whether
    /// <c>Delivery</c> is described with its declared names.
    /// </summary>
    [Fact]
    public void configuring_nothing_scans_the_entry_assembly() {
        Check.That(Registrations().Contains(typeof(Delivery))).IsTrue();
    }

    /// <summary>
    /// The scan passes by the enum in that same assembly that declares no contract, rather than
    /// adopting it on the way past.
    /// </summary>
    [Fact]
    public void a_scan_adopts_no_enum_that_declares_nothing() {
        Check.That(Registrations().Contains(typeof(PlainLevel))).IsFalse();
    }

    private static EnumMemberNameBindingRegistrations Registrations() {
        ServiceCollection services = new();
        services.AddControllers().AddEnumMemberNameBinding();

        using ServiceProvider provider = services.BuildServiceProvider();

        return provider.GetRequiredService<EnumMemberNameBindingRegistrations>();
    }

}
