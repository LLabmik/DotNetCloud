using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace DotNetCloud.Core.Data.Configuration.Shared;

/// <summary>
/// Shared JSON converter and value comparer for <c>ICollection&lt;Guid&gt;</c> role ID columns.
/// Used by both <see cref="Organizations.OrganizationMemberConfiguration"/> and
/// <see cref="Organizations.TeamMemberConfiguration"/>.
/// </summary>
public static class RoleIdsConversion
{
    public static readonly ValueConverter<ICollection<Guid>, string> Converter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

    public static readonly ValueComparer<ICollection<Guid>> Comparer = new(
        (c1, c2) => c1!.SequenceEqual(c2!),
        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
        c => (ICollection<Guid>)c.ToList());
}
