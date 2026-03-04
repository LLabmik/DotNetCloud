using DotNetCloud.Core.Data.Entities.Organizations;

namespace DotNetCloud.Integration.Tests.Builders;

/// <summary>
/// Fluent builder for creating <see cref="Organization"/> test instances.
/// </summary>
internal sealed class OrganizationBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Organization";
    private string? _description = "An organization created for integration tests.";
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _isDeleted;
    private DateTime? _deletedAt;

    public OrganizationBuilder WithId(Guid id) { _id = id; return this; }
    public OrganizationBuilder WithName(string name) { _name = name; return this; }
    public OrganizationBuilder WithDescription(string? desc) { _description = desc; return this; }
    public OrganizationBuilder WithCreatedAt(DateTime dt) { _createdAt = dt; return this; }
    public OrganizationBuilder AsDeleted(DateTime? deletedAt = null)
    {
        _isDeleted = true;
        _deletedAt = deletedAt ?? DateTime.UtcNow;
        return this;
    }

    public Organization Build()
    {
        return new Organization
        {
            Id = _id,
            Name = _name,
            Description = _description,
            CreatedAt = _createdAt,
            IsDeleted = _isDeleted,
            DeletedAt = _deletedAt,
        };
    }
}
