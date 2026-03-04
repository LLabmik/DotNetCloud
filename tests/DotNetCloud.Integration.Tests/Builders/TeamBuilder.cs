using DotNetCloud.Core.Data.Entities.Organizations;

namespace DotNetCloud.Integration.Tests.Builders;

/// <summary>
/// Fluent builder for creating <see cref="Team"/> test instances.
/// </summary>
internal sealed class TeamBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _organizationId = Guid.NewGuid();
    private string _name = "Test Team";
    private string? _description;
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _isDeleted;
    private DateTime? _deletedAt;

    public TeamBuilder WithId(Guid id) { _id = id; return this; }
    public TeamBuilder WithOrganizationId(Guid orgId) { _organizationId = orgId; return this; }
    public TeamBuilder WithName(string name) { _name = name; return this; }
    public TeamBuilder WithDescription(string? desc) { _description = desc; return this; }
    public TeamBuilder WithCreatedAt(DateTime dt) { _createdAt = dt; return this; }
    public TeamBuilder AsDeleted(DateTime? deletedAt = null)
    {
        _isDeleted = true;
        _deletedAt = deletedAt ?? DateTime.UtcNow;
        return this;
    }

    public Team Build()
    {
        return new Team
        {
            Id = _id,
            OrganizationId = _organizationId,
            Name = _name,
            Description = _description,
            CreatedAt = _createdAt,
            IsDeleted = _isDeleted,
            DeletedAt = _deletedAt,
        };
    }
}
