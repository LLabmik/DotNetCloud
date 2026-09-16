using System.Globalization;
using DotNetCloud.Client.Android.Converters;

namespace DotNetCloud.Client.Android.Tests.Converters;

/// <summary>
/// Covers the folder filter chip highlight. The chips used to be rendered by
/// <c>BindableLayout</c>'s default label (the item's <c>ToString()</c>) because the layout had no
/// item template; these tests pin the chip identity + selected-state semantics.
/// </summary>
[TestClass]
public sealed class FolderChipBackgroundConverterTests
{
    private static readonly Guid FinanceFolderId = Guid.Parse("01a0a6cb-9d53-7122-9e1e-1f3947d5de6d");

    private static object Convert(params object?[] values) =>
        new FolderChipBackgroundConverter().Convert(values!, typeof(Color), null!, CultureInfo.InvariantCulture);

    [TestMethod]
    public void Convert_ChipIsTheActiveFilter_HighlightsTheChip()
    {
        Assert.AreEqual(FolderChipBackgroundConverter.HighlightColor, Convert(FinanceFolderId, FinanceFolderId));
    }

    [TestMethod]
    public void Convert_AnotherFolderIsFiltered_LeavesTheChipInactive()
    {
        Assert.AreEqual(
            FolderChipBackgroundConverter.InactiveColor,
            Convert(FinanceFolderId, Guid.NewGuid()));
    }

    [TestMethod]
    public void Convert_AllNotesChip_IsHighlightedOnlyWhileNoFolderIsFiltered()
    {
        Assert.AreEqual(FolderChipBackgroundConverter.HighlightColor, Convert(null, null));
        Assert.AreEqual(FolderChipBackgroundConverter.InactiveColor, Convert(null, FinanceFolderId));
    }

    [TestMethod]
    public void Convert_NoFolderFiltered_LeavesFolderChipsInactive()
    {
        Assert.AreEqual(FolderChipBackgroundConverter.InactiveColor, Convert(FinanceFolderId, null));
    }

    [TestMethod]
    public void Convert_MissingValues_TreatsTheChipAsAllNotes()
    {
        Assert.AreEqual(FolderChipBackgroundConverter.HighlightColor, Convert());
    }
}
