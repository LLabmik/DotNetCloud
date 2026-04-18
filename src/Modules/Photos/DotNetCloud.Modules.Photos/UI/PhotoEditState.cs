using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Photos.UI;

/// <summary>
/// Tracks the visual edit state for a photo in the lightbox.
/// Builds CSS transform/filter strings from accumulated edits.
/// </summary>
internal sealed class PhotoEditState
{
    /// <summary>Cumulative rotation in degrees (0, 90, 180, 270).</summary>
    public int Rotation { get; private set; }

    /// <summary>Whether the image is flipped horizontally.</summary>
    public bool FlipH { get; private set; }

    /// <summary>Whether the image is flipped vertically.</summary>
    public bool FlipV { get; private set; }

    /// <summary>Brightness adjustment (-100 to 100).</summary>
    public int Brightness { get; private set; }

    /// <summary>Contrast adjustment (-100 to 100).</summary>
    public int Contrast { get; private set; }

    /// <summary>Saturation adjustment (-100 to 100).</summary>
    public int Saturation { get; private set; }

    /// <summary>Blur radius in pixels (0+).</summary>
    public int BlurRadius { get; private set; }

    /// <summary>Sharpen level (0+).</summary>
    public int Sharpen { get; private set; }

    /// <summary>Applies a single edit operation to the local state.</summary>
    public void Apply(PhotoEditType editType, int value)
    {
        switch (editType)
        {
            case PhotoEditType.Rotate:
                Rotation = (Rotation + value) % 360;
                break;
            case PhotoEditType.Flip:
                if (value == 0) FlipH = !FlipH;
                else FlipV = !FlipV;
                break;
            case PhotoEditType.Brightness:
                Brightness = value;
                break;
            case PhotoEditType.Contrast:
                Contrast = value;
                break;
            case PhotoEditType.Saturation:
                Saturation = value;
                break;
            case PhotoEditType.Blur:
                BlurRadius = value;
                break;
            case PhotoEditType.Sharpen:
                Sharpen = value;
                break;
        }
    }

    /// <summary>Resets all edit state to defaults.</summary>
    public void Reset()
    {
        Rotation = 0;
        FlipH = false;
        FlipV = false;
        Brightness = 0;
        Contrast = 0;
        Saturation = 0;
        BlurRadius = 0;
        Sharpen = 0;
    }

    /// <summary>Replays a sequence of edit operations into this state (after reset).</summary>
    public void Rebuild(IEnumerable<PhotoEditOperationDto> operations)
    {
        Reset();
        foreach (var op in operations)
        {
            if (op.Parameters.TryGetValue("value", out var valStr) && int.TryParse(valStr, out var val))
            {
                Apply(op.OperationType, val);
            }
        }
    }

    /// <summary>
    /// Builds an inline CSS style string for transform and filter properties.
    /// Returns empty string when no edits are active.
    /// </summary>
    public string GetImageStyle()
    {
        var transforms = new List<string>();
        if (Rotation != 0)
            transforms.Add($"rotate({Rotation}deg)");
        if (FlipH)
            transforms.Add("scaleX(-1)");
        if (FlipV)
            transforms.Add("scaleY(-1)");

        var filters = new List<string>();
        if (Brightness != 0)
            filters.Add($"brightness({100 + Brightness}%)");
        if (Contrast != 0)
            filters.Add($"contrast({100 + Contrast}%)");
        if (Saturation != 0)
            filters.Add($"saturate({100 + Saturation}%)");
        if (BlurRadius > 0)
            filters.Add($"blur({BlurRadius}px)");

        var parts = new List<string>();
        if (transforms.Count > 0)
            parts.Add($"transform: {string.Join(' ', transforms)}");
        if (filters.Count > 0)
            parts.Add($"filter: {string.Join(' ', filters)}");

        return parts.Count > 0 ? string.Join("; ", parts) : string.Empty;
    }
}
