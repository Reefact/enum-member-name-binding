using System.ComponentModel;
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

    [Fact]
    public void configuring_nothing_scans_the_entry_assembly() {
        ServiceCollection services = new();

        services.AddControllers().AddEnumMemberNameBinding();

        // Only the scan could have reached this one, and only this package's converter refuses the C#
        // name: the stock EnumConverter parses it, case-insensitively at that.
        TypeConverter delivery = TypeDescriptor.GetConverter(typeof(Delivery));
        Check.That(delivery.ConvertFromString("express")).IsEqualTo(Delivery.Express);
        Check.ThatCode(() => delivery.ConvertFromString(nameof(Delivery.Express))).Throws<FormatException>();
    }

    /// <summary>
    /// The scan passes by the enum in that same assembly that declares no contract, rather than
    /// adopting it on the way past.
    /// </summary>
    /// <remarks>
    /// Asserted on a numeric value because that is where the two converters part company: the stock
    /// one accepts it, and this package's never does.
    /// </remarks>
    [Fact]
    public void a_scan_adopts_no_enum_that_declares_nothing() {
        ServiceCollection services = new();

        services.AddControllers().AddEnumMemberNameBinding();

        Check.That(TypeDescriptor.GetConverter(typeof(PlainLevel)).ConvertFromString("0")).IsEqualTo(PlainLevel.Low);
    }

}
