namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// DTO for swimlane transition rules returned by the API.
/// </summary>
public sealed class SwimlaneTransitionRuleDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid FromSwimlaneId { get; set; }
    public Guid ToSwimlaneId { get; set; }
    public bool IsAllowed { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for setting a single transition rule in the matrix.
/// </summary>
public sealed class SetTransitionRuleDto
{
    public Guid FromSwimlaneId { get; set; }
    public Guid ToSwimlaneId { get; set; }
    public bool IsAllowed { get; set; }
}
