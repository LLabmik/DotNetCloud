namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// One folder entry shown as a picker item or filter chip. <see cref="Id"/> is <c>null</c> for the
/// "no folder" entry — the editor's "None (unfiled)" option and the notes list's "All Notes" chip.
/// </summary>
/// <param name="Id">Target folder, or <c>null</c> to leave the note unfiled / clear the filter.</param>
/// <param name="Name">Display label.</param>
public sealed record NoteFolderOption(Guid? Id, string Name)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}
