using System.Reflection;
using DotNetCloud.Modules.About;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.About.UI;

/// <summary>Code-behind for the <see cref="AboutPage"/> component.</summary>
public partial class AboutPage : ComponentBase
{
    private bool _sidebarCollapsed;
    private string _activeSection = "overview";

    private string _informationalVersion = "0.0.0";
    private string _fileVersion = "0.0.0";
    private string _productName = "DotNetCloud";
    private string _runtimeVersion = "";
    private string _osDescription = "";
    private string _processArchitecture = "";

    private static readonly IReadOnlyList<(string Name, string License)> _packages = new[]
    {
        ("AngleSharp", "BSD-3-Clause"),
        ("AspNetCore.HealthChecks.*", "Apache-2.0"),
        ("Avalonia", "MIT"),
        ("CommunityToolkit.Mvvm", "MIT"),
        ("coverlet.collector", "MIT"),
        ("Cronos", "Apache-2.0"),
        ("DiffPlex", "Apache-2.0"),
        ("DocumentFormat.OpenXml", "MIT"),
        ("Google.Apis.*", "Apache-2.0"),
        ("Google.Protobuf", "BSD-3-Clause"),
        ("Grpc.*", "Apache-2.0"),
        ("HtmlSanitizer", "MIT"),
        ("LTRData.FuseDotNet", "MIT"),
        ("MailKit", "MIT"),
        ("Markdig", "BSD-2-Clause"),
        ("Microsoft.AspNetCore.*", "MIT"),
        ("Microsoft.EntityFrameworkCore.*", "MIT"),
        ("Microsoft.Extensions.*", "MIT"),
        ("Microsoft.Maui.Controls.*", "MIT"),
        ("Microsoft.NET.Test.Sdk", "MIT"),
        ("Moq", "BSD-3-Clause"),
        ("MSTest.*", "MIT"),
        ("Npgsql.EntityFrameworkCore.PostgreSQL", "PostgreSQL"),
        ("NPOI", "Apache-2.0"),
        ("OpenIddict.*", "Apache-2.0"),
        ("OpenTelemetry.*", "Apache-2.0"),
        ("Otp.NET", "MIT"),
        ("PdfPig", "Apache-2.0"),
        ("Scalar.AspNetCore", "MIT"),
        ("Serilog & Extensions", "Apache-2.0"),
        ("SixLabors.ImageSharp", "Apache-2.0"),
        ("sqlite-net-pcl", "MIT"),
        ("System.CommandLine", "MIT"),
        ("System.Management", "MIT"),
        ("System.Security.Cryptography.Xml", "MIT"),
        ("TagLibSharp", "LGPL-2.1-only"),
        ("Tmds.DBus.Protocol", "MIT"),
        ("Xamarin.Firebase.Messaging", "Apache-2.0"),
        ("Yarp.ReverseProxy", "MIT"),
    };

    protected override async Task OnInitializedAsync()
    {
        var entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        _informationalVersion = entryAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        _fileVersion = entryAssembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version ?? "0.0.0";

        _productName = entryAssembly
            .GetCustomAttribute<AssemblyProductAttribute>()?
            .Product ?? "DotNetCloud";

        _runtimeVersion = Environment.Version.ToString();
        _osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        _processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();

        try
        {
            var collapsed = await JSRuntime.InvokeAsync<string>("localStorage.getItem",
                new object?[] { "dotnetcloud.sidebar:about" });
            if (bool.TryParse(collapsed ?? "false", out var parsed))
                _sidebarCollapsed = parsed;
        }
        catch { }
    }

    private void ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        try
        {
            _ = JSRuntime.InvokeVoidAsync("localStorage.setItem",
                new object?[] { "dotnetcloud.sidebar:about", _sidebarCollapsed.ToString().ToLowerInvariant() });
        }
        catch { }
    }

    private void SetSection(string section)
    {
        _activeSection = section;
    }
}
