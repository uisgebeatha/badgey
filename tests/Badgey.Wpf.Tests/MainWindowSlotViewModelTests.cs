using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey.Wpf.Tests;

public class MainWindowSlotViewModelTests
{
    [Fact]
    public void Constructor_CreatesEightOrderedSlotsWithFirstSelected()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(Enumerable.Range(1, 8), viewModel.Slots.Select(slot => slot.SlotNumber));
        Assert.Same(viewModel.Slots[0], viewModel.SelectedSlot);
        Assert.Equal("Hello Badgey", viewModel.Message);
        Assert.True(viewModel.Slots[0].IsPopulated);
        Assert.All(viewModel.Slots.Skip(1), slot => Assert.False(slot.IsPopulated));
        Assert.True(viewModel.SendCommand.CanExecute(parameter: null));
        Assert.StartsWith("1 of 8 slots", viewModel.UploadSummaryText);
    }

    [Fact]
    public void SelectingSlots_RestoresIndependentEditorAndPreviewState()
    {
        var viewModel = CreateViewModel();
        var staticMode = viewModel.Modes.Single(
            option => option.Value == BadgeDisplayMode.StaticCentered);
        var firstSlot = viewModel.Slots[0];
        var secondSlot = viewModel.Slots[1];

        viewModel.Message = "FIRST";
        viewModel.SelectedMode = staticMode;
        viewModel.Speed = 7;
        var firstPreview = viewModel.PreviewBitmap;

        viewModel.SelectedSlot = secondSlot;

        Assert.Equal(string.Empty, viewModel.Message);
        Assert.Equal(BadgeDisplayMode.ScrollLeft, viewModel.SelectedMode.Value);
        Assert.Equal(4, viewModel.Speed);

        viewModel.Message = "SECOND";
        viewModel.Speed = 2;
        var secondPreview = viewModel.PreviewBitmap;

        viewModel.SelectedSlot = firstSlot;

        Assert.Equal("FIRST", viewModel.Message);
        Assert.Equal(BadgeDisplayMode.StaticCentered, viewModel.SelectedMode.Value);
        Assert.Equal(7, viewModel.Speed);
        Assert.Same(firstPreview, viewModel.PreviewBitmap);
        Assert.NotSame(secondPreview, viewModel.PreviewBitmap);
        Assert.StartsWith("2 of 8 slots", viewModel.UploadSummaryText);
    }

    [Fact]
    public void ContentDraftsAndSettings_RemainScopedToTheirSlot()
    {
        var viewModel = CreateViewModel();
        var graphic = viewModel.ContentTypes.Single(
            option => option.Value == BadgeContentKind.Graphic);
        var text = viewModel.ContentTypes.Single(
            option => option.Value == BadgeContentKind.Text);

        viewModel.Message = "Text draft";
        viewModel.SelectedContent = graphic;
        viewModel.Speed = 8;

        Assert.False(viewModel.SelectedSlot.IsPopulated);
        Assert.Equal("Text draft", viewModel.Message);

        viewModel.SelectedSlot = viewModel.Slots[1];

        Assert.Equal(BadgeContentKind.Text, viewModel.SelectedContent.Value);
        Assert.Equal(4, viewModel.Speed);

        viewModel.SelectedSlot = viewModel.Slots[0];

        Assert.Equal(BadgeContentKind.Graphic, viewModel.SelectedContent.Value);
        Assert.Equal(8, viewModel.Speed);
        Assert.Equal("Text draft", viewModel.Message);

        viewModel.SelectedContent = text;

        Assert.True(viewModel.SelectedSlot.IsPopulated);
        Assert.Equal("Text draft", viewModel.Message);
    }

    [Fact]
    public void GraphicInversion_RemainsScopedToItsSlot()
    {
        var viewModel = CreateViewModel();
        var graphic = viewModel.ContentTypes.Single(
            option => option.Value == BadgeContentKind.Graphic);
        var firstSlot = viewModel.Slots[0];
        var secondSlot = viewModel.Slots[1];
        var firstBitmap = new MonochromeBitmap(width: 8, height: 11)
        {
            [0, 0] = true,
        };
        var secondBitmap = new MonochromeBitmap(width: 8, height: 11)
        {
            [1, 0] = true,
        };

        firstSlot.LoadGraphic(firstBitmap, "first.png");
        viewModel.SelectedContent = graphic;
        viewModel.IsGraphicInverted = true;

        viewModel.SelectedSlot = secondSlot;
        secondSlot.LoadGraphic(secondBitmap, "second.png");
        viewModel.SelectedContent = graphic;

        Assert.False(viewModel.IsGraphicInverted);
        Assert.Equal("second.png", viewModel.SelectedFileName);

        viewModel.SelectedSlot = firstSlot;

        Assert.True(viewModel.IsGraphicInverted);
        Assert.True(viewModel.PreviewRepeatGapIsLit);
        Assert.Equal("first.png", viewModel.SelectedFileName);
        Assert.False(viewModel.PreviewBitmap[0, 0]);
    }

    [Fact]
    public void ClearSelectedSlot_ResetsOnlyThatSlotAndUpdatesUploadState()
    {
        var viewModel = CreateViewModel();
        var firstSlot = viewModel.Slots[0];
        var secondSlot = viewModel.Slots[1];

        viewModel.Message = "FIRST";
        viewModel.Speed = 7;
        viewModel.SelectedSlot = secondSlot;
        viewModel.Message = "SECOND";

        viewModel.SelectedSlot = firstSlot;
        viewModel.ClearSelectedSlot();

        Assert.Equal(string.Empty, viewModel.Message);
        Assert.Equal(BadgeContentKind.Text, viewModel.SelectedContent.Value);
        Assert.Equal(BadgeDisplayMode.ScrollLeft, viewModel.SelectedMode.Value);
        Assert.Equal(4, viewModel.Speed);
        Assert.False(firstSlot.IsPopulated);
        Assert.Equal("SECOND", secondSlot.Message);
        Assert.True(secondSlot.IsPopulated);
        Assert.StartsWith("1 of 8 slots", viewModel.UploadSummaryText);
        Assert.True(viewModel.SendCommand.CanExecute(parameter: null));

        viewModel.SelectedSlot = secondSlot;
        viewModel.ClearSelectedSlot();

        Assert.False(viewModel.SendCommand.CanExecute(parameter: null));
        Assert.Equal("0 of 8 slots • Nothing to send", viewModel.UploadSummaryText);
    }

    private static MainWindowViewModel CreateViewModel() =>
        new(
            new BadgeController(
                new WindowsTextRenderer(),
                new WindowsImageRenderer(),
                new WindowsGifAnimationRenderer()));
}
