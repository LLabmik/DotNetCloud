namespace DotNetCloud.Modules.Example.Web.Services;

/// <summary>
/// A note as the Example module's own REST API returns it.
/// </summary>
/// <param name="Id">The note's id.</param>
/// <param name="Title">The note's title.</param>
/// <param name="Content">The note's body.</param>
/// <param name="CreatedAt">When the note was created.</param>
/// <param name="UpdatedAt">When the note was last changed, if ever.</param>
public sealed record ExampleNoteDto(
    Guid Id,
    string Title,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
