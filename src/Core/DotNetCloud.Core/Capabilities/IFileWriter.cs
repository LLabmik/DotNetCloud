namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Lets a module store a file in the acting user's Files tree.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Restricted — the install requires an explicit grant, and every call also
/// requires the user's consent to the <c>files:write</c> scope.
/// </para>
/// <para>
/// <b>Deliberate minimalism.</b> This is not a filesystem: it creates content the user asked to keep
/// (a downloaded attachment, an exported report) and nothing else. Renaming, moving, deleting and
/// folder creation stay with the Files module's own UI, because a module that can delete a user's
/// files needs a much stronger review than a module that can add one.
/// </para>
/// <para>
/// <b>No caller-supplied identity.</b> The user is taken from the verified end-user assertion on the
/// call; a module can never pass a user id. Reading files is <see cref="IFileDirectory"/>.
/// </para>
/// </remarks>
public interface IFileWriter : ICapabilityInterface
{
    /// <summary>
    /// Saves content into a folder of the acting user's files.
    /// </summary>
    /// <param name="parentId">Target folder; <see langword="null"/> saves into the user's root.</param>
    /// <param name="fileName">File name. The core sanitizes it and the Files module stores it as given.</param>
    /// <param name="contentType">MIME type of the content.</param>
    /// <param name="content">The bytes to store; read to the end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created node, or a new version of the existing file with that name.</returns>
    Task<FileNodeInfo> SaveAsync(
        Guid? parentId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);
}
