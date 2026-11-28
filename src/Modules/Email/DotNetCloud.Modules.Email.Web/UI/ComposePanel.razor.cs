using DotNetCloud.Modules.Email.Web.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace DotNetCloud.Modules.Email.Web.UI;

/// <summary>
/// Code-behind for the compose form.
/// </summary>
/// <remarks>
/// Attachments are staged on the module host before the message is sent: a browser file is uploaded
/// through <c>upload-attachment</c>, one of the user's cloud files is copied through
/// <c>attachments/from-file</c>. Both land in the same store, so the send payload only carries keys.
/// </remarks>
public partial class ComposePanel : EmailPanelBase
{
    private const long MaxBrowserFileBytes = 25L * 1024 * 1024;

    private void Discard()
    {
        State.Compose = null;
        State.View = EmailView.Mailbox;
    }

    private async Task OnFilesSelected(InputFileChangeEventArgs args)
    {
        foreach (var file in args.GetMultipleFiles())
        {
            try
            {
                await using var stream = file.OpenReadStream(MaxBrowserFileBytes);
                await State.UploadAttachmentAsync(stream, file.Name, file.ContentType);
            }
            catch (IOException)
            {
                State.ReportError($"Could not read \"{file.Name}\": it is larger than the 25 MB attachment limit.");
            }
        }
    }

    private async Task BrowseFilesAsync()
    {
        var choice = await State.PickFromFilesAsync(FileBrowserMode.File);

        if (choice?.Node is { } node)
        {
            await State.AttachFromFileAsync(node);
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:F1} KB";
        }

        return bytes < 1024L * 1024 * 1024
            ? $"{bytes / (1024d * 1024):F1} MB"
            : $"{bytes / (1024d * 1024 * 1024):F2} GB";
    }
}
