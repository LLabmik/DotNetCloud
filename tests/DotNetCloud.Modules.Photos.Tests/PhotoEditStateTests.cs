using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.UI;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class PhotoEditStateTests
{
    private PhotoEditState _state = null!;

    [TestInitialize]
    public void Setup() => _state = new PhotoEditState();

    // ─── Initial State ───────────────────────────────────────────────

    [TestMethod]
    public void NewState_AllDefaultsAreZeroOrFalse()
    {
        Assert.AreEqual(0, _state.Rotation);
        Assert.IsFalse(_state.FlipH);
        Assert.IsFalse(_state.FlipV);
        Assert.AreEqual(0, _state.Brightness);
        Assert.AreEqual(0, _state.Contrast);
        Assert.AreEqual(0, _state.Saturation);
        Assert.AreEqual(0, _state.BlurRadius);
        Assert.AreEqual(0, _state.Sharpen);
    }

    [TestMethod]
    public void NewState_GetImageStyle_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, _state.GetImageStyle());
    }

    // ─── Apply: Rotate ───────────────────────────────────────────────

    [TestMethod]
    public void Apply_Rotate90_SetsRotationTo90()
    {
        _state.Apply(PhotoEditType.Rotate, 90);

        Assert.AreEqual(90, _state.Rotation);
    }

    [TestMethod]
    public void Apply_Rotate180_SetsRotationTo180()
    {
        _state.Apply(PhotoEditType.Rotate, 180);

        Assert.AreEqual(180, _state.Rotation);
    }

    [TestMethod]
    public void Apply_Rotate270_SetsRotationTo270()
    {
        _state.Apply(PhotoEditType.Rotate, 270);

        Assert.AreEqual(270, _state.Rotation);
    }

    [TestMethod]
    public void Apply_RotateTwice90_AccumulatesTo180()
    {
        _state.Apply(PhotoEditType.Rotate, 90);
        _state.Apply(PhotoEditType.Rotate, 90);

        Assert.AreEqual(180, _state.Rotation);
    }

    [TestMethod]
    public void Apply_RotateFourTimes90_WrapsToZero()
    {
        for (int i = 0; i < 4; i++)
            _state.Apply(PhotoEditType.Rotate, 90);

        Assert.AreEqual(0, _state.Rotation);
    }

    [TestMethod]
    public void Apply_Rotate90Then270_WrapsToZero()
    {
        _state.Apply(PhotoEditType.Rotate, 90);
        _state.Apply(PhotoEditType.Rotate, 270);

        Assert.AreEqual(0, _state.Rotation);
    }

    // ─── Apply: Flip ─────────────────────────────────────────────────

    [TestMethod]
    public void Apply_FlipH_TogglesFlipHOn()
    {
        _state.Apply(PhotoEditType.Flip, 0);

        Assert.IsTrue(_state.FlipH);
        Assert.IsFalse(_state.FlipV);
    }

    [TestMethod]
    public void Apply_FlipV_TogglesFlipVOn()
    {
        _state.Apply(PhotoEditType.Flip, 1);

        Assert.IsFalse(_state.FlipH);
        Assert.IsTrue(_state.FlipV);
    }

    [TestMethod]
    public void Apply_FlipHTwice_TogglesBackOff()
    {
        _state.Apply(PhotoEditType.Flip, 0);
        _state.Apply(PhotoEditType.Flip, 0);

        Assert.IsFalse(_state.FlipH);
    }

    [TestMethod]
    public void Apply_FlipVTwice_TogglesBackOff()
    {
        _state.Apply(PhotoEditType.Flip, 1);
        _state.Apply(PhotoEditType.Flip, 1);

        Assert.IsFalse(_state.FlipV);
    }

    [TestMethod]
    public void Apply_FlipHAndV_BothTrue()
    {
        _state.Apply(PhotoEditType.Flip, 0);
        _state.Apply(PhotoEditType.Flip, 1);

        Assert.IsTrue(_state.FlipH);
        Assert.IsTrue(_state.FlipV);
    }

    // ─── Apply: Brightness/Contrast/Saturation ──────────────────────

    [TestMethod]
    public void Apply_Brightness_SetsValue()
    {
        _state.Apply(PhotoEditType.Brightness, 50);

        Assert.AreEqual(50, _state.Brightness);
    }

    [TestMethod]
    public void Apply_BrightnessNegative_SetsNegativeValue()
    {
        _state.Apply(PhotoEditType.Brightness, -75);

        Assert.AreEqual(-75, _state.Brightness);
    }

    [TestMethod]
    public void Apply_BrightnessTwice_LastValueWins()
    {
        _state.Apply(PhotoEditType.Brightness, 30);
        _state.Apply(PhotoEditType.Brightness, -20);

        Assert.AreEqual(-20, _state.Brightness);
    }

    [TestMethod]
    public void Apply_Contrast_SetsValue()
    {
        _state.Apply(PhotoEditType.Contrast, 40);

        Assert.AreEqual(40, _state.Contrast);
    }

    [TestMethod]
    public void Apply_Saturation_SetsValue()
    {
        _state.Apply(PhotoEditType.Saturation, -30);

        Assert.AreEqual(-30, _state.Saturation);
    }

    // ─── Apply: Blur/Sharpen ─────────────────────────────────────────

    [TestMethod]
    public void Apply_Blur_SetsBlurRadius()
    {
        _state.Apply(PhotoEditType.Blur, 5);

        Assert.AreEqual(5, _state.BlurRadius);
    }

    [TestMethod]
    public void Apply_Sharpen_SetsSharpenLevel()
    {
        _state.Apply(PhotoEditType.Sharpen, 50);

        Assert.AreEqual(50, _state.Sharpen);
    }

    // ─── Reset ───────────────────────────────────────────────────────

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        _state.Apply(PhotoEditType.Rotate, 90);
        _state.Apply(PhotoEditType.Flip, 0);
        _state.Apply(PhotoEditType.Flip, 1);
        _state.Apply(PhotoEditType.Brightness, 50);
        _state.Apply(PhotoEditType.Contrast, -30);
        _state.Apply(PhotoEditType.Saturation, 25);
        _state.Apply(PhotoEditType.Blur, 5);
        _state.Apply(PhotoEditType.Sharpen, 50);

        _state.Reset();

        Assert.AreEqual(0, _state.Rotation);
        Assert.IsFalse(_state.FlipH);
        Assert.IsFalse(_state.FlipV);
        Assert.AreEqual(0, _state.Brightness);
        Assert.AreEqual(0, _state.Contrast);
        Assert.AreEqual(0, _state.Saturation);
        Assert.AreEqual(0, _state.BlurRadius);
        Assert.AreEqual(0, _state.Sharpen);
    }

    [TestMethod]
    public void Reset_GetImageStyle_ReturnsEmpty()
    {
        _state.Apply(PhotoEditType.Rotate, 180);
        _state.Apply(PhotoEditType.Brightness, 50);

        _state.Reset();

        Assert.AreEqual(string.Empty, _state.GetImageStyle());
    }

    // ─── Rebuild ─────────────────────────────────────────────────────

    [TestMethod]
    public void Rebuild_EmptyList_ResetsState()
    {
        _state.Apply(PhotoEditType.Rotate, 90);

        _state.Rebuild([]);

        Assert.AreEqual(0, _state.Rotation);
    }

    [TestMethod]
    public void Rebuild_SingleRotate_SetsCorrectState()
    {
        var ops = new List<PhotoEditOperationDto>
        {
            MakeOp(PhotoEditType.Rotate, 270)
        };

        _state.Rebuild(ops);

        Assert.AreEqual(270, _state.Rotation);
    }

    [TestMethod]
    public void Rebuild_MultipleOps_AccumulatesCorrectly()
    {
        var ops = new List<PhotoEditOperationDto>
        {
            MakeOp(PhotoEditType.Rotate, 90),
            MakeOp(PhotoEditType.Rotate, 90),
            MakeOp(PhotoEditType.Flip, 0),
            MakeOp(PhotoEditType.Brightness, 30),
            MakeOp(PhotoEditType.Contrast, -20),
            MakeOp(PhotoEditType.Blur, 3)
        };

        _state.Rebuild(ops);

        Assert.AreEqual(180, _state.Rotation);
        Assert.IsTrue(_state.FlipH);
        Assert.IsFalse(_state.FlipV);
        Assert.AreEqual(30, _state.Brightness);
        Assert.AreEqual(-20, _state.Contrast);
        Assert.AreEqual(3, _state.BlurRadius);
    }

    [TestMethod]
    public void Rebuild_OverwritesPreviousValues()
    {
        var ops = new List<PhotoEditOperationDto>
        {
            MakeOp(PhotoEditType.Brightness, 50),
            MakeOp(PhotoEditType.Brightness, -10)
        };

        _state.Rebuild(ops);

        Assert.AreEqual(-10, _state.Brightness);
    }

    [TestMethod]
    public void Rebuild_ClearsExistingStateFirst()
    {
        _state.Apply(PhotoEditType.Rotate, 270);
        _state.Apply(PhotoEditType.Flip, 0);
        _state.Apply(PhotoEditType.Contrast, 80);

        _state.Rebuild(new List<PhotoEditOperationDto>
        {
            MakeOp(PhotoEditType.Brightness, 10)
        });

        Assert.AreEqual(0, _state.Rotation);
        Assert.IsFalse(_state.FlipH);
        Assert.AreEqual(0, _state.Contrast);
        Assert.AreEqual(10, _state.Brightness);
    }

    [TestMethod]
    public void Rebuild_IgnoresOpsWithMissingValueKey()
    {
        var badOp = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Rotate,
            Parameters = new Dictionary<string, string> { ["degrees"] = "90" } // "degrees" not "value"
        };

        _state.Rebuild([badOp]);

        Assert.AreEqual(0, _state.Rotation);
    }

    [TestMethod]
    public void Rebuild_IgnoresOpsWithNonIntegerValue()
    {
        var badOp = new PhotoEditOperationDto
        {
            OperationType = PhotoEditType.Brightness,
            Parameters = new Dictionary<string, string> { ["value"] = "abc" }
        };

        _state.Rebuild([badOp]);

        Assert.AreEqual(0, _state.Brightness);
    }

    // ─── GetImageStyle: Transform ────────────────────────────────────

    [TestMethod]
    public void GetImageStyle_RotateOnly_ReturnsTransform()
    {
        _state.Apply(PhotoEditType.Rotate, 90);

        var style = _state.GetImageStyle();

        Assert.AreEqual("transform: rotate(90deg)", style);
    }

    [TestMethod]
    public void GetImageStyle_Rotate180_ReturnsCorrectDegrees()
    {
        _state.Apply(PhotoEditType.Rotate, 180);

        var style = _state.GetImageStyle();

        Assert.AreEqual("transform: rotate(180deg)", style);
    }

    [TestMethod]
    public void GetImageStyle_FlipHOnly_ReturnsScaleX()
    {
        _state.Apply(PhotoEditType.Flip, 0);

        var style = _state.GetImageStyle();

        Assert.AreEqual("transform: scaleX(-1)", style);
    }

    [TestMethod]
    public void GetImageStyle_FlipVOnly_ReturnsScaleY()
    {
        _state.Apply(PhotoEditType.Flip, 1);

        var style = _state.GetImageStyle();

        Assert.AreEqual("transform: scaleY(-1)", style);
    }

    [TestMethod]
    public void GetImageStyle_RotateAndFlipH_CombinesTransforms()
    {
        _state.Apply(PhotoEditType.Rotate, 90);
        _state.Apply(PhotoEditType.Flip, 0);

        var style = _state.GetImageStyle();

        Assert.AreEqual("transform: rotate(90deg) scaleX(-1)", style);
    }

    [TestMethod]
    public void GetImageStyle_RotateAndFlipBoth_CombinesAllTransforms()
    {
        _state.Apply(PhotoEditType.Rotate, 270);
        _state.Apply(PhotoEditType.Flip, 0);
        _state.Apply(PhotoEditType.Flip, 1);

        var style = _state.GetImageStyle();

        Assert.AreEqual("transform: rotate(270deg) scaleX(-1) scaleY(-1)", style);
    }

    // ─── GetImageStyle: Filter ───────────────────────────────────────

    [TestMethod]
    public void GetImageStyle_BrightnessOnly_ReturnsFilter()
    {
        _state.Apply(PhotoEditType.Brightness, 50);

        var style = _state.GetImageStyle();

        Assert.AreEqual("filter: brightness(150%)", style);
    }

    [TestMethod]
    public void GetImageStyle_BrightnessNegative_ReturnsLowerPercentage()
    {
        _state.Apply(PhotoEditType.Brightness, -50);

        var style = _state.GetImageStyle();

        Assert.AreEqual("filter: brightness(50%)", style);
    }

    [TestMethod]
    public void GetImageStyle_ContrastOnly_ReturnsFilter()
    {
        _state.Apply(PhotoEditType.Contrast, 30);

        var style = _state.GetImageStyle();

        Assert.AreEqual("filter: contrast(130%)", style);
    }

    [TestMethod]
    public void GetImageStyle_SaturationOnly_ReturnsFilter()
    {
        _state.Apply(PhotoEditType.Saturation, -60);

        var style = _state.GetImageStyle();

        Assert.AreEqual("filter: saturate(40%)", style);
    }

    [TestMethod]
    public void GetImageStyle_BlurOnly_ReturnsFilter()
    {
        _state.Apply(PhotoEditType.Blur, 5);

        var style = _state.GetImageStyle();

        Assert.AreEqual("filter: blur(5px)", style);
    }

    [TestMethod]
    public void GetImageStyle_MultipleFilters_CombinesInOrder()
    {
        _state.Apply(PhotoEditType.Brightness, 20);
        _state.Apply(PhotoEditType.Contrast, -10);
        _state.Apply(PhotoEditType.Saturation, 30);
        _state.Apply(PhotoEditType.Blur, 3);

        var style = _state.GetImageStyle();

        Assert.AreEqual("filter: brightness(120%) contrast(90%) saturate(130%) blur(3px)", style);
    }

    // ─── GetImageStyle: Transform + Filter Combined ──────────────────

    [TestMethod]
    public void GetImageStyle_TransformAndFilter_SemicolonSeparated()
    {
        _state.Apply(PhotoEditType.Rotate, 90);
        _state.Apply(PhotoEditType.Brightness, 40);

        var style = _state.GetImageStyle();

        Assert.AreEqual("transform: rotate(90deg); filter: brightness(140%)", style);
    }

    [TestMethod]
    public void GetImageStyle_FullEditStack_ProducesCompleteStyle()
    {
        _state.Apply(PhotoEditType.Rotate, 180);
        _state.Apply(PhotoEditType.Flip, 0);
        _state.Apply(PhotoEditType.Brightness, 25);
        _state.Apply(PhotoEditType.Contrast, -15);
        _state.Apply(PhotoEditType.Saturation, 50);
        _state.Apply(PhotoEditType.Blur, 2);

        var style = _state.GetImageStyle();

        Assert.AreEqual(
            "transform: rotate(180deg) scaleX(-1); filter: brightness(125%) contrast(85%) saturate(150%) blur(2px)",
            style);
    }

    // ─── GetImageStyle: Edge Cases ───────────────────────────────────

    [TestMethod]
    public void GetImageStyle_BlurZero_NotIncludedInFilter()
    {
        _state.Apply(PhotoEditType.Blur, 0);

        Assert.AreEqual(string.Empty, _state.GetImageStyle());
    }

    [TestMethod]
    public void GetImageStyle_SharpenDoesNotAffectCssFilter()
    {
        // Sharpen has no CSS equivalent; it only affects server-side processing
        _state.Apply(PhotoEditType.Sharpen, 50);

        Assert.AreEqual(string.Empty, _state.GetImageStyle());
    }

    [TestMethod]
    public void GetImageStyle_AfterResetFollowedByNewEdits_OnlyShowsNewEdits()
    {
        _state.Apply(PhotoEditType.Rotate, 180);
        _state.Apply(PhotoEditType.Brightness, 50);
        _state.Reset();
        _state.Apply(PhotoEditType.Contrast, 20);

        Assert.AreEqual("filter: contrast(120%)", _state.GetImageStyle());
    }

    // ─── Compound Scenarios ──────────────────────────────────────────

    [TestMethod]
    public void CompoundScenario_RotateFlipUndoViaRebuild()
    {
        // Simulate: rotate 90 → flip H → undo last (flip H removed)
        _state.Rebuild(new List<PhotoEditOperationDto>
        {
            MakeOp(PhotoEditType.Rotate, 90)
            // flip H was undone, so it's not in the stack
        });

        Assert.AreEqual(90, _state.Rotation);
        Assert.IsFalse(_state.FlipH);
        Assert.AreEqual("transform: rotate(90deg)", _state.GetImageStyle());
    }

    [TestMethod]
    public void CompoundScenario_AdjustBrightnessMultipleTimes()
    {
        // Each brightness change replaces the prior value
        _state.Apply(PhotoEditType.Brightness, 30);
        Assert.AreEqual("filter: brightness(130%)", _state.GetImageStyle());

        _state.Apply(PhotoEditType.Brightness, -20);
        Assert.AreEqual("filter: brightness(80%)", _state.GetImageStyle());

        _state.Apply(PhotoEditType.Brightness, 0);
        Assert.AreEqual(string.Empty, _state.GetImageStyle());
    }

    [TestMethod]
    public void CompoundScenario_FlipHThenRotate_BothReflected()
    {
        _state.Apply(PhotoEditType.Flip, 0);
        _state.Apply(PhotoEditType.Rotate, 270);

        var style = _state.GetImageStyle();

        Assert.AreEqual("transform: rotate(270deg) scaleX(-1)", style);
    }

    [TestMethod]
    public void CompoundScenario_FullWorkflow_ApplyResetRebuild()
    {
        // Step 1: apply some edits manually
        _state.Apply(PhotoEditType.Rotate, 90);
        _state.Apply(PhotoEditType.Brightness, 40);
        Assert.AreEqual("transform: rotate(90deg); filter: brightness(140%)", _state.GetImageStyle());

        // Step 2: reset (simulates closing lightbox)
        _state.Reset();
        Assert.AreEqual(string.Empty, _state.GetImageStyle());

        // Step 3: rebuild from server stack (simulates reopening lightbox)
        _state.Rebuild(new List<PhotoEditOperationDto>
        {
            MakeOp(PhotoEditType.Rotate, 90),
            MakeOp(PhotoEditType.Brightness, 40)
        });
        Assert.AreEqual("transform: rotate(90deg); filter: brightness(140%)", _state.GetImageStyle());
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static PhotoEditOperationDto MakeOp(PhotoEditType type, int value) => new()
    {
        OperationType = type,
        Parameters = new Dictionary<string, string> { ["value"] = value.ToString() }
    };
}
