using System.Linq.Expressions;
using System.Reflection;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data.Models;

namespace DotNetCloud.Modules.Search.Services;

internal static class SearchVisibilityFilterBuilder
{
    private static readonly string GroupVisibilityMarker =
        $"\"{SearchVisibilityMetadata.VisibilityScopeKey}\":\"{SearchVisibilityMetadata.VisibilityScopeGroupMembers}\"";

    private static readonly MethodInfo StringContainsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])
        ?? throw new InvalidOperationException("Unable to resolve string.Contains(string) method.");

    internal static IQueryable<SearchIndexEntry> Apply(IQueryable<SearchIndexEntry> source, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);

        var entryParameter = Expression.Parameter(typeof(SearchIndexEntry), "entry");
        var metadataJsonExpression = Expression.Property(entryParameter, nameof(SearchIndexEntry.MetadataJson));
        var ownerExpression = Expression.Equal(
            Expression.Property(entryParameter, nameof(SearchIndexEntry.OwnerId)),
            Expression.Constant(query.UserId));

        Expression combinedExpression = ownerExpression;
        var distinctGroupIds = query.GroupIds.Distinct().ToArray();
        if (distinctGroupIds.Length > 0)
        {
            var metadataNotNullExpression = Expression.NotEqual(
                metadataJsonExpression,
                Expression.Constant(null, typeof(string)));
            var visibilityMarkerExpression = Expression.Call(
                metadataJsonExpression,
                StringContainsMethod,
                Expression.Constant(GroupVisibilityMarker));

            Expression? groupScopeExpression = null;
            foreach (var groupId in distinctGroupIds)
            {
                var groupTokenExpression = Expression.Call(
                    metadataJsonExpression,
                    StringContainsMethod,
                    Expression.Constant($"|{groupId:D}|"));
                groupScopeExpression = groupScopeExpression is null
                    ? groupTokenExpression
                    : Expression.OrElse(groupScopeExpression, groupTokenExpression);
            }

            if (groupScopeExpression is not null)
            {
                var sharedVisibilityExpression = Expression.AndAlso(
                    metadataNotNullExpression,
                    Expression.AndAlso(visibilityMarkerExpression, groupScopeExpression));
                combinedExpression = Expression.OrElse(combinedExpression, sharedVisibilityExpression);
            }
        }

        var predicate = Expression.Lambda<Func<SearchIndexEntry, bool>>(combinedExpression, entryParameter);
        return source.Where(predicate);
    }
}