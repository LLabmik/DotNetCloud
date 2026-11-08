using DotNetCloud.Core.DTOs;
using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using DotNetCloud.UI.Shared.Services;

namespace DotNetCloud.UI.Web.Client.Pages.Admin;

/// <summary>
/// Code-behind for the admin module store page: upload a <c>.dncpkg</c>, review what it asks for,
/// then install it.
/// </summary>
/// <remarks>
/// The review is deliberately a two-step flow. "Install" sends back the hash and the exact
/// capability and scope lists the review displayed; the server refuses a package whose bytes changed
/// or whose manifest requests anything the administrator did not see.
/// </remarks>
public partial class Store : ComponentBase
{
    /// <summary>Matches the server's upload limit for a package.</summary>
    private const long MaxUploadBytes = 512L * 1024 * 1024;

    [Inject]
    private DotNetCloudApiClient Api { get; set; } = default!;

    [Inject]
    private ToastService Toast { get; set; } = default!;

    private bool _loadingCatalogue = true;
    private bool _inspecting;
    private bool _installing;
    private bool _acknowledged;
    private bool _unverifiedAcknowledged;

    private IBrowserFile? _selectedFile;
    private ModulePackageReviewDto? _review;
    private ModuleInstallResultDto? _result;
    private IReadOnlyList<ModuleStorePackageDto> _catalogue = [];

    /// <summary>The store package the current review belongs to, when it came from the catalogue.</summary>
    private string? _storeModuleId;

    /// <summary>The store version being reviewed or installed.</summary>
    private string? _storeVersion;

    private string ReviewTitle =>
        string.Join(' ', new[] { _review?.Name, _review?.Version }.Where(part => !string.IsNullOrWhiteSpace(part)));

    private string SignatureLabel => _review switch
    {
        null => "unknown",
        { IsSigned: false } => "unsigned",
        { IsSignedByTrustedKey: true } => $"signed ({_review.KeyId})",
        _ => $"untrusted key ({_review.KeyId})",
    };

    /// <summary>Both approvals are required, and a structurally invalid package is never installable.</summary>
    private bool CanInstall =>
        !_installing
        && _review is not null
        && _review.IsStructurallyValid
        && _acknowledged
        && (_review.IsSignedByTrustedKey || _unverifiedAcknowledged);

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        await LoadCatalogueAsync();
    }

    private async Task LoadCatalogueAsync()
    {
        _loadingCatalogue = true;

        try
        {
            _catalogue = await Api.ListStorePackagesAsync();
        }
        finally
        {
            _loadingCatalogue = false;
        }
    }

    private async Task OnFileSelected(InputFileChangeEventArgs args)
    {
        _result = null;
        _review = null;
        _acknowledged = false;
        _unverifiedAcknowledged = false;
        _storeModuleId = null;
        _storeVersion = null;
        _selectedFile = args.File;

        if (_selectedFile.Size > MaxUploadBytes)
        {
            Toast.ShowError("The package is larger than the 512 MB limit.");
            _selectedFile = null;
            return;
        }

        _inspecting = true;

        try
        {
            await using var stream = _selectedFile.OpenReadStream(MaxUploadBytes);
            _review = await Api.InspectModulePackageAsync(stream, _selectedFile.Name);

            if (_review is null)
            {
                Toast.ShowError("The server could not read the package.");
                _selectedFile = null;
            }
            else if (!_review.IsStructurallyValid)
            {
                Toast.ShowError("The package failed verification. See the findings below.");
            }
            else if (!_review.IsSignedByTrustedKey)
            {
                Toast.ShowWarning("The package is not signed by a key this server trusts.");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Toast.ShowError($"Could not read the file: {ex.Message}");
            _selectedFile = null;
        }
        finally
        {
            _inspecting = false;
        }
    }

    private async Task InstallAsync()
    {
        var review = _review;
        var selectedFile = _selectedFile;

        if (review is null || (selectedFile is null && _storeModuleId is null))
        {
            return;
        }

        if (_storeModuleId is not null)
        {
            await InstallStorePackageAsync();
            return;
        }

        if (selectedFile is null)
        {
            return;
        }

        _installing = true;
        StateHasChanged();

        try
        {
            await using var stream = selectedFile.OpenReadStream(MaxUploadBytes);

            _result = await Api.InstallModulePackageAsync(
                stream,
                selectedFile.Name,
                new InstallModulePackageDto
                {
                    ExpectedSha256 = review.Sha256,
                    AcknowledgedCapabilities = review.Capabilities,
                    AcknowledgedScopes = review.UserConsentScopes,
                    AllowUnverified = !review.IsSignedByTrustedKey,
                    StartAfterInstall = true,
                });

            if (_result?.Success == true)
            {
                Toast.ShowSuccess($"Installed {@_result.ModuleId} {@_result.Version}.");
                _review = null;
                _selectedFile = null;
                _acknowledged = false;
                _unverifiedAcknowledged = false;
                await LoadCatalogueAsync();
            }
            else
            {
                Toast.ShowError(_result?.Error ?? "The install failed.");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Toast.ShowError($"Could not read the file: {ex.Message}");
        }
        finally
        {
            _installing = false;
        }
    }

    private void ClearAsync()
    {
        _review = null;
        _selectedFile = null;
        _result = null;
        _acknowledged = false;
        _unverifiedAcknowledged = false;
        _storeModuleId = null;
        _storeVersion = null;
    }

    /// <summary>
    /// Fetches a catalogue package and shows the same review the upload flow uses, so the approval
    /// (hash + exact capability and scope lists) is identical whichever way the bytes arrived.
    /// </summary>
    private async Task ReviewStorePackageAsync(ModuleStorePackageDto package)
    {
        _result = null;
        _review = null;
        _acknowledged = false;
        _unverifiedAcknowledged = false;
        _selectedFile = null;
        _storeModuleId = package.ModuleId;
        _storeVersion = package.Version;
        _inspecting = true;
        StateHasChanged();

        try
        {
            _review = await Api.InspectStorePackageAsync(package.ModuleId, package.Version);

            if (_review is null)
            {
                Toast.ShowError($"Could not fetch {package.ModuleId} {package.Version} from the store.");
                _storeModuleId = null;
                _storeVersion = null;
            }
            else if (!_review.IsStructurallyValid)
            {
                Toast.ShowError("The package failed verification. See the findings below.");
            }
            else if (!_review.IsSignedByTrustedKey)
            {
                Toast.ShowWarning("The package is not signed by a key this server trusts.");
            }
        }
        finally
        {
            _inspecting = false;
        }
    }

    private async Task InstallStorePackageAsync()
    {
        if (_review is null || _storeModuleId is null)
        {
            return;
        }

        _installing = true;
        StateHasChanged();

        try
        {
            _result = await Api.InstallStorePackageAsync(
                _storeModuleId,
                new InstallStorePackageDto
                {
                    Version = _storeVersion,
                    ExpectedSha256 = _review.Sha256,
                    AcknowledgedCapabilities = _review.Capabilities,
                    AcknowledgedScopes = _review.UserConsentScopes,
                    AllowUnverified = !_review.IsSignedByTrustedKey,
                    StartAfterInstall = true,
                });

            if (_result?.Success == true)
            {
                Toast.ShowSuccess($"Installed {@_result.ModuleId} {@_result.Version}.");
                _review = null;
                _storeModuleId = null;
                _storeVersion = null;
                _acknowledged = false;
                _unverifiedAcknowledged = false;
                await LoadCatalogueAsync();
            }
            else
            {
                Toast.ShowError(_result?.Error ?? "The install failed.");
            }
        }
        finally
        {
            _installing = false;
        }
    }
}
