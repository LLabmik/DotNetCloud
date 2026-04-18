using System.Text;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from PPTX presentations using Open XML SDK.
/// Supports .pptx, .pptm, and .potx files.
/// </summary>
public sealed class PptxContentExtractor : IContentExtractor
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.ms-powerpoint.presentation.macroEnabled.12",
        "application/vnd.openxmlformats-officedocument.presentationml.template"
    };

    /// <inheritdoc />
    public bool CanExtract(string mimeType)
    {
        return SupportedMimeTypes.Contains(mimeType);
    }

    /// <inheritdoc />
    public Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var metadata = new Dictionary<string, string>();

        using var document = PresentationDocument.Open(fileStream, false);
        var presentationPart = document.PresentationPart;

        if (presentationPart?.Presentation?.SlideIdList is null)
        {
            return Task.FromResult<ExtractedContent?>(null);
        }

        // Extract core properties
        var props = document.PackageProperties;
        if (props.Creator is { } author && !string.IsNullOrWhiteSpace(author))
            metadata["author"] = author;
        if (props.Title is { } title && !string.IsNullOrWhiteSpace(title))
            metadata["title"] = title;

        var slideCount = 0;
        foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<SlideId>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            slideCount++;

            var relationshipId = slideId.RelationshipId?.Value;
            if (relationshipId is null) continue;

            if (presentationPart.GetPartById(relationshipId) is SlidePart slidePart)
            {
                ExtractSlideText(slidePart, sb);
            }
        }

        // Also extract from notes slides
        foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<SlideId>())
        {
            var relationshipId = slideId.RelationshipId?.Value;
            if (relationshipId is null) continue;

            if (presentationPart.GetPartById(relationshipId) is SlidePart slidePart &&
                slidePart.NotesSlidePart is { } notesPart)
            {
                var notesText = notesPart.NotesSlide?.InnerText;
                if (!string.IsNullOrWhiteSpace(notesText))
                {
                    sb.AppendLine(notesText);
                }
            }
        }

        metadata["slideCount"] = slideCount.ToString();

        var result = new ExtractedContent
        {
            Text = sb.ToString(),
            Metadata = metadata
        };

        return Task.FromResult<ExtractedContent?>(result);
    }

    private static void ExtractSlideText(SlidePart slidePart, StringBuilder sb)
    {
        var slide = slidePart.Slide;
        if (slide?.CommonSlideData?.ShapeTree is null) return;

        foreach (var shape in slide.CommonSlideData.ShapeTree.Elements<Shape>())
        {
            var textBody = shape.TextBody;
            if (textBody is null) continue;

            foreach (var paragraph in textBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>())
            {
                var text = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }
        }
    }
}
