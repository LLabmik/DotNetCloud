namespace DotNetCloud.Core.DTOs.Home
{
    /// <summary>
    /// One registered Home-page widget, as supplied by the caller's catalog. This intentionally carries
    /// only what ordering needs, so <c>DotNetCloud.Core</c> never depends on UI types.
    /// </summary>
    /// <param name="ModuleId">The module identifier that owns the widget.</param>
    /// <param name="SortOrder">The catalog's default sort order for the widget.</param>
    public sealed record HomeWidgetCatalogEntry(string ModuleId, int SortOrder);

    /// <summary>
    /// One widget slot in a user's resolved Home-page layout.
    /// </summary>
    /// <param name="ModuleId">The module identifier that owns the widget.</param>
    /// <param name="Visible">Whether the widget should be rendered.</param>
    public sealed record HomeWidgetLayoutItem(string ModuleId, bool Visible);
}

namespace DotNetCloud.Core.Services
{
    using DotNetCloud.Core.DTOs.Home;

    /// <summary>
    /// Resolves a user's widget order and visibility against the registered widget catalog.
    /// Pure and deterministic so it can be unit-tested without Blazor and reused by other clients.
    /// </summary>
    public static class HomeWidgetLayoutResolver
    {
        /// <summary>
        /// Produces the user's full layout: every catalog widget, in the user's order, with its
        /// resolved visibility. Widgets the user has never arranged keep their catalog order and
        /// appear after arranged ones.
        /// </summary>
        /// <param name="catalog">The registered widgets, in catalog order.</param>
        /// <param name="preferences">The user's persisted preferences, or <see langword="null"/>.</param>
        /// <returns>The resolved layout, including hidden widgets so callers can offer them for re-enabling.</returns>
        public static IReadOnlyList<HomeWidgetLayoutItem> Resolve(
            IReadOnlyList<HomeWidgetCatalogEntry> catalog,
            HomeWidgetPreferences? preferences)
        {
            ArgumentNullException.ThrowIfNull(catalog);

            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (preferences?.Items is not null)
            {
                for (var i = 0; i < preferences.Items.Count; i++)
                {
                    var item = preferences.Items[i];
                    if (item is null || string.IsNullOrWhiteSpace(item.ModuleId))
                    {
                        continue;
                    }

                    var moduleId = item.ModuleId.Trim();

                    // First occurrence wins, so a duplicated entry cannot shuffle the layout.
                    if (!order.ContainsKey(moduleId))
                    {
                        order[moduleId] = i;
                    }

                    if (!item.Visible)
                    {
                        hidden.Add(moduleId);
                    }
                }
            }

            return catalog
                .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.ModuleId))
                .Select((entry, ordinal) => new
                {
                    ModuleId = entry.ModuleId.Trim(),
                    entry.SortOrder,
                    Ordinal = ordinal,
                })
                .OrderBy(x => order.TryGetValue(x.ModuleId, out var index) ? index : int.MaxValue)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Ordinal)
                .Select(x => new HomeWidgetLayoutItem(x.ModuleId, !hidden.Contains(x.ModuleId)))
                .ToList();
        }

        /// <summary>
        /// Builds the payload to persist from a resolved layout. Slots for modules that are currently
        /// uninstalled are carried over from <paramref name="existing"/>, so a later reinstall restores
        /// their position instead of appending them.
        /// </summary>
        /// <param name="styleToken">The selected style token; unknown values normalize to the default.</param>
        /// <param name="layout">The layout to persist, in display order.</param>
        /// <param name="existing">The previously persisted preferences, if any.</param>
        /// <returns>The preferences to persist.</returns>
        public static HomeWidgetPreferences BuildPreferences(
            string? styleToken,
            IReadOnlyList<HomeWidgetLayoutItem> layout,
            HomeWidgetPreferences? existing = null)
        {
            ArgumentNullException.ThrowIfNull(layout);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<HomeWidgetPreferenceItem>(layout.Count);

            foreach (var entry in layout)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.ModuleId))
                {
                    continue;
                }

                var moduleId = entry.ModuleId.Trim();
                if (!seen.Add(moduleId))
                {
                    continue;
                }

                items.Add(new HomeWidgetPreferenceItem { ModuleId = moduleId, Visible = entry.Visible });
            }

            if (existing?.Items is not null)
            {
                foreach (var prior in existing.Items)
                {
                    if (prior is null || string.IsNullOrWhiteSpace(prior.ModuleId))
                    {
                        continue;
                    }

                    var moduleId = prior.ModuleId.Trim();
                    if (!seen.Add(moduleId))
                    {
                        continue;
                    }

                    items.Add(new HomeWidgetPreferenceItem { ModuleId = moduleId, Visible = prior.Visible });
                }
            }

            return new HomeWidgetPreferences
            {
                Version = HomeWidgetPreferences.CurrentVersion,
                Style = HomeWidgetStyles.Normalize(styleToken),
                Items = items,
            };
        }
    }
}
