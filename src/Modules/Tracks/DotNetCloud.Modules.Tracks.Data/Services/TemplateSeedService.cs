using System.Text.Json;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Seeds built-in product templates when first accessed.
/// Idempotent — skips seeding if templates already exist.
/// </summary>
public sealed class TemplateSeedService
{
    private readonly TracksDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly List<(string Name, string Description, string Category, string DefinitionJson)> BuiltInTemplates =
    [
        (
            "Software Project",
            "Classic software development workflow with backlog, progress tracking, and review stages.",
            "Software",
            SerializeDefinition([
                new("Backlog", "#6b7280", 1000, false),
                new("To Do", "#3b82f6", 2000, false),
                new("In Progress", "#f59e0b", 3000, false),
                new("Review", "#8b5cf6", 4000, false),
                new("Done", "#10b981", 5000, true)
            ])
        ),
        (
            "Bug Tracker",
            "Dedicated bug tracking workflow from report to resolution.",
            "Software",
            SerializeDefinition([
                new("Reported", "#ef4444", 1000, false),
                new("Triaged", "#f97316", 2000, false),
                new("In Fix", "#f59e0b", 3000, false),
                new("Testing", "#3b82f6", 4000, false),
                new("Resolved", "#10b981", 5000, true)
            ])
        ),
        (
            "Content Calendar",
            "Editorial content pipeline for blogs, social media, videos, and newsletters.",
            "Marketing",
            SerializeDefinition([
                new("Ideas", "#a855f7", 1000, false),
                new("Drafting", "#3b82f6", 2000, false),
                new("Review", "#f59e0b", 3000, false),
                new("Scheduled", "#06b6d4", 4000, false),
                new("Published", "#10b981", 5000, true)
            ])
        ),
        (
            "Simple Todo",
            "Minimalist to-do list. Perfect for personal tasks or small projects.",
            "General",
            SerializeDefinition([
                new("To Do", "#6b7280", 1000, false),
                new("Doing", "#f59e0b", 2000, false),
                new("Done", "#10b981", 3000, true)
            ])
        ),
        (
            "Hiring Pipeline",
            "Recruitment workflow from sourcing to offer.",
            "HR",
            SerializeDefinition([
                new("Sourced", "#a855f7", 1000, false),
                new("Phone Screen", "#3b82f6", 2000, false),
                new("Onsite", "#f59e0b", 3000, false),
                new("Offer", "#10b981", 4000, false),
                new("Hired", "#22c55e", 5000, true)
            ])
        )
    ];

    public TemplateSeedService(TracksDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Ensures built-in templates are seeded. Safe to call multiple times.
    /// </summary>
    public async Task EnsureSeededAsync(CancellationToken ct)
    {
        var anyExist = await _db.ProductTemplates
            .AnyAsync(t => t.IsBuiltIn, ct);

        if (anyExist)
            return;

        var now = DateTime.UtcNow;

        foreach (var (name, description, category, definitionJson) in BuiltInTemplates)
        {
            // Check if this specific template already exists (by name, to handle partial seeds)
            var exists = await _db.ProductTemplates
                .AnyAsync(t => t.Name == name && t.IsBuiltIn, ct);

            if (exists)
                continue;

            _db.ProductTemplates.Add(new ProductTemplate
            {
                Name = name,
                Description = description,
                Category = category,
                IsBuiltIn = true,
                CreatedByUserId = Guid.Empty, // System-seeded
                DefinitionJson = definitionJson,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string SerializeDefinition(List<SwimlaneDef> swimlanes)
    {
        return JsonSerializer.Serialize(
            new { swimlanes = swimlanes.Select(s => new { title = s.Title, color = s.Color, position = s.Position, isDone = s.IsDone }) },
            JsonOptions);
    }

    private sealed record SwimlaneDef(string Title, string Color, int Position, bool IsDone);
}
