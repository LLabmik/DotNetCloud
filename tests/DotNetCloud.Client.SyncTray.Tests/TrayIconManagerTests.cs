using DotNetCloud.Client.SyncTray;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Tests;

[TestClass]
public sealed class TrayIconManagerTests
{
    [TestMethod]
    public void GetChatBadgeKind_WhenNoUnreadAndNoMentions_ReturnsNone()
    {
        var result = TrayIconManager.GetChatBadgeKind(chatUnreadCount: 0, chatHasMentions: false);
        Assert.AreEqual(TrayIconManager.TrayChatBadgeKind.None, result);
    }

    [TestMethod]
    public void GetChatBadgeKind_WhenUnreadWithoutMentions_ReturnsUnread()
    {
        var result = TrayIconManager.GetChatBadgeKind(chatUnreadCount: 3, chatHasMentions: false);
        Assert.AreEqual(TrayIconManager.TrayChatBadgeKind.Unread, result);
    }

    [TestMethod]
    public void GetChatBadgeKind_WhenMentionsPresent_ReturnsMention()
    {
        var result = TrayIconManager.GetChatBadgeKind(chatUnreadCount: 1, chatHasMentions: true);
        Assert.AreEqual(TrayIconManager.TrayChatBadgeKind.Mention, result);
    }

    [TestMethod]
    public void GetChatBadgeKind_WhenMentionsTrueAndUnreadZero_ReturnsMention()
    {
        var result = TrayIconManager.GetChatBadgeKind(chatUnreadCount: 0, chatHasMentions: true);
        Assert.AreEqual(TrayIconManager.TrayChatBadgeKind.Mention, result);
    }

    // ── Symbol rendering verification tests ───────────────────────────

    private const int Size = 32;
    private const float Centre = (Size - 1) / 2f; // 15.5
    private const float Radius = Centre - 1f;      // 14.5

    /// <summary>
    /// Creates a pixel buffer pre-filled with a coloured circle (simulating what
    /// CreateCircleBitmap does before calling DrawStatusSymbol).
    /// </summary>
    private static byte[] CreateCircleBuffer(byte r, byte g, byte b)
    {
        var pixels = new byte[Size * Size * 4];

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                float dx = px - Centre;
                float dy = py - Centre;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                int idx = (py * Size + px) * 4;
                if (dist <= Radius)
                {
                    pixels[idx + 0] = b;
                    pixels[idx + 1] = g;
                    pixels[idx + 2] = r;
                    pixels[idx + 3] = 255;
                }
            }
        }

        return pixels;
    }

    /// <summary>
    /// Counts pixels where the white channel contribution is significant (R,G,B all > 200, full alpha).
    /// </summary>
    private static int CountWhiteishPixels(byte[] pixels)
    {
        int count = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte pb = pixels[i + 0];
            byte pg = pixels[i + 1];
            byte pr = pixels[i + 2];
            byte pa = pixels[i + 3];
            if (pa == 255 && pr > 200 && pg > 200 && pb > 200)
                count++;
        }
        return count;
    }

    [TestMethod]
    [DataRow(TrayState.Idle, DisplayName = "Idle_Checkmark")]
    [DataRow(TrayState.Syncing, DisplayName = "Syncing_Arrows")]
    [DataRow(TrayState.Paused, DisplayName = "Paused_Bars")]
    [DataRow(TrayState.Error, DisplayName = "Error_XMark")]
    [DataRow(TrayState.Conflict, DisplayName = "Conflict_Exclamation")]
    [DataRow(TrayState.Offline, DisplayName = "Offline_Dash")]
    public void DrawStatusSymbol_AllStates_ProduceWhiteSymbolPixels(TrayState state)
    {
        // Start with a dark circle (0x40, 0x40, 0x40) so white pixels are easy to detect.
        var pixels = CreateCircleBuffer(0x40, 0x40, 0x40);

        TrayIconManager.DrawStatusSymbol(pixels, Size, Centre, Radius, state);

        var whiteCount = CountWhiteishPixels(pixels);

        // Each symbol should produce a meaningful number of white pixels (at least 20).
        Assert.IsTrue(whiteCount >= 20,
            $"State {state}: Expected ≥20 white symbol pixels, got {whiteCount}. Symbol may not be rendering.");
    }

    [TestMethod]
    public void DrawStatusSymbol_SymbolsStayWithinCircle_NoArtifactsOutside()
    {
        foreach (var state in new[] { TrayState.Idle, TrayState.Syncing, TrayState.Paused, TrayState.Error, TrayState.Conflict, TrayState.Offline })
        {
            var pixels = new byte[Size * Size * 4]; // All transparent initially.

            TrayIconManager.DrawStatusSymbol(pixels, Size, Centre, Radius, state);

            // Corners: (0,0), (31,0), (0,31), (31,31) should remain transparent.
            foreach (var (x, y) in new[] { (0, 0), (31, 0), (0, 31), (31, 31) })
            {
                int idx = (y * Size + x) * 4;
                byte alpha = pixels[idx + 3];
                Assert.AreEqual(0, alpha,
                    $"State {state}: Corner ({x},{y}) alpha={alpha}. Symbol bleeds outside circle.");
            }
        }
    }

    [TestMethod]
    public void DrawStatusSymbol_CheckmarkSymmetry_HasPixelsInBothLegs()
    {
        var pixels = CreateCircleBuffer(0x40, 0x40, 0x40);
        TrayIconManager.DrawStatusSymbol(pixels, Size, Centre, Radius, TrayState.Idle);

        // Short leg is in the lower-left quadrant (~x:8-12, y:16-20)
        // Long leg is in the upper-right quadrant (~x:12-22, y:10-20)
        bool hasLeftLeg = false;
        bool hasRightLeg = false;

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                int idx = (py * Size + px) * 4;
                if (pixels[idx + 0] > 200 && pixels[idx + 1] > 200 && pixels[idx + 2] > 200 && pixels[idx + 3] == 255)
                {
                    if (px < 12) hasLeftLeg = true;
                    if (px > 18) hasRightLeg = true;
                }
            }
        }

        Assert.IsTrue(hasLeftLeg, "Checkmark missing short leg (left side).");
        Assert.IsTrue(hasRightLeg, "Checkmark missing long leg (right side).");
    }

    [TestMethod]
    public void DrawStatusSymbol_PauseBars_HasTwoDistinctVerticalRegions()
    {
        var pixels = CreateCircleBuffer(0x40, 0x40, 0x40);
        TrayIconManager.DrawStatusSymbol(pixels, Size, Centre, Radius, TrayState.Paused);

        // Bars are at x ≈ centre - 3.5 (12) and x ≈ centre + 3.5 (19).
        // The gap between (x=14 to x=17) should have no white pixels at y = centre.
        int gapWhiteCount = 0;
        for (int px = 14; px <= 17; px++)
        {
            int idx = ((int)Centre * Size + px) * 4;
            if (pixels[idx + 0] > 200 && pixels[idx + 1] > 200 && pixels[idx + 2] > 200 && pixels[idx + 3] == 255)
                gapWhiteCount++;
        }

        Assert.AreEqual(0, gapWhiteCount, "There should be a gap between the two pause bars.");
    }
}
