using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Messages;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Services;
using NeoEditor.Plugins.ImageTools.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.ViewModels;

/// <summary>
/// Create-image document: import → pending list, AI candidates → staged pending
/// entries, multi-select → one OpenImageDocumentMessage per item, and the lazy
/// cleanup of staged AI files (deselect deletes, leftover files stay until the
/// next document build).
/// </summary>
public class ImageCreateDocumentTests
{
    static ImageCreateDocumentTests()
    {
        // Preview tests decode Avalonia Bitmaps — the headless platform (Skia) is required.
        TestApp.EnsureAvaloniaInitialized();
    }

    private static ILocalizationService CreateLocMock()
    {
        var loc = new Mock<ILocalizationService>();
        loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        return loc.Object;
    }

    private static ImageCreateDocument CreateDoc(IMessenger? messenger = null,
        IImageGenerationService? imageGen = null)
    {
        var loc = CreateLocMock();
        return new ImageCreateDocument(
            loc,
            new ImageFileService(loc),
            new AiGenerationPanelViewModel(
                imageGen ?? new Mock<IImageGenerationService>().Object,
                loc,
                Mock.Of<IConfigService>(c => c.Config == new NeoEditor.Core.Model.AppConfig())),
            messenger ?? new Mock<IMessenger>().Object);
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var img = new SixLabors.ImageSharp.Image<Rgba32>(width, height);
        img.Mutate(ctx => ctx.BackgroundColor(new Rgba32(255, 0, 0, 255)));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public void ImportImages_AddsPickedPaths_AndSkipsDuplicates()
    {
        var doc = CreateDoc();
        var pathA = Path.Combine(Path.GetTempPath(), "a.png");
        var pathB = Path.Combine(Path.GetTempPath(), "b.png");
        var fileService = new Mock<IImageFileService>();
        fileService.Setup(s => s.PickImagesAsync(It.IsAny<bool>()))
            .ReturnsAsync([pathA, pathB, pathA]);

        // Rebuild with the mocked picker.
        var loc = CreateLocMock();
        var doc2 = new ImageCreateDocument(
            loc,
            fileService.Object,
            new AiGenerationPanelViewModel(
                new Mock<IImageGenerationService>().Object,
                loc,
                Mock.Of<IConfigService>(c => c.Config == new NeoEditor.Core.Model.AppConfig())),
            new Mock<IMessenger>().Object);

        doc2.ImportImagesCommand.Execute(null);

        Assert.Equal(2, doc2.PendingItems.Count);
        Assert.Equal("a.png", doc2.PendingItems[0].DisplayName);
        Assert.Equal(PendingImageKind.Imported, doc2.PendingItems[0].Kind);
        Assert.True(doc2.HasPendingItems);
        Assert.False(doc2.CanOpenPending); // nothing selected yet
    }

    [Fact]
    public async Task CandidateGenerated_StagesFile_AndAddsPendingItem()
    {
        var gen = new Mock<IImageGenerationService>();
        gen.Setup(g => g.IsAvailable).Returns(true);
        gen.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<ImageGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageGenerationResult(CreatePngBytes(8, 8), "png", 8, 8, null));

        var doc = CreateDoc(imageGen: gen.Object);
        doc.AiPanel.AiPrompt = "a sword";
        doc.AiPanel.CandidateCount = 1;

        await doc.AiPanel.GenerateCommand.ExecuteAsync(null);

        var item = Assert.Single(doc.PendingItems);
        Assert.Equal(PendingImageKind.AiGenerated, item.Kind);
        Assert.Equal("ai_candidate_1.png", item.DisplayName);
        Assert.True(File.Exists(item.Path)); // staged file exists on disk
    }

    [Fact]
    public void OpenPending_SendsOneMessagePerSelectedItem_AndKeepsItems()
    {
        // IMessenger.Send is an extension method (not mockable) — capture via a real messenger.
        var messenger = WeakReferenceMessenger.Default;
        var received = new List<OpenImageDocumentMessage>();
        messenger.Register<OpenImageDocumentMessage>(this, (_, msg) => received.Add(msg));

        try
        {
            var doc = CreateDoc(messenger);

            var itemA = new PendingImageItem
            {
                Path = Path.Combine(Path.GetTempPath(), "a.png"),
                DisplayName = "a.png",
                Kind = PendingImageKind.Imported
            };
            var itemB = new PendingImageItem
            {
                Path = Path.Combine(Path.GetTempPath(), "b.png"),
                DisplayName = "b.png",
                Kind = PendingImageKind.Imported
            };
            var itemC = new PendingImageItem
            {
                Path = Path.Combine(Path.GetTempPath(), "c.png"),
                DisplayName = "c.png",
                Kind = PendingImageKind.Imported
            };
            doc.PendingItems.Add(itemA);
            doc.PendingItems.Add(itemB);
            doc.PendingItems.Add(itemC);

            itemA.IsSelected = true;
            itemB.IsSelected = true;
            Assert.True(doc.CanOpenPending);

            doc.OpenPendingCommand.Execute(null);

            // One OpenImageDocumentMessage per selected item; items stay in the list.
            Assert.Equal(2, received.Count);
            Assert.Contains(received, msg => msg.ImagePath == itemA.Path);
            Assert.Contains(received, msg => msg.ImagePath == itemB.Path);

            Assert.Equal(3, doc.PendingItems.Count);
        }
        finally
        {
            messenger.Unregister<OpenImageDocumentMessage>(this);
        }
    }

    [Fact]
    public void OpenItem_SendsMessage_AndKeepsItem()
    {
        var messenger = WeakReferenceMessenger.Default;
        var received = new List<OpenImageDocumentMessage>();
        messenger.Register<OpenImageDocumentMessage>(this, (_, msg) => received.Add(msg));

        try
        {
            var doc = CreateDoc(messenger);
            var item = new PendingImageItem
            {
                Path = Path.Combine(Path.GetTempPath(), "a.png"),
                DisplayName = "a.png",
                Kind = PendingImageKind.Imported
            };
            doc.PendingItems.Add(item);

            doc.OpenItem(item);

            Assert.Single(received);
            Assert.Equal(item.Path, received[0].ImagePath);
            Assert.Contains(item, doc.PendingItems); // double-click open keeps the item
        }
        finally
        {
            messenger.Unregister<OpenImageDocumentMessage>(this);
        }
    }

    [Fact]
    public void AddPendingFiles_QueuesPaths_AndSkipsDuplicates()
    {
        var doc = CreateDoc();
        var pathA = Path.Combine(Path.GetTempPath(), "a.png");
        var pathB = Path.Combine(Path.GetTempPath(), "b.png");

        doc.AddPendingFiles([pathA, pathB, pathA]);

        Assert.Equal(2, doc.PendingItems.Count);
        Assert.All(doc.PendingItems, item => Assert.Equal(PendingImageKind.Imported, item.Kind));
    }

    [Fact]
    public void SelectingItem_DecodesFile_IntoPreviewPane()
    {
        var doc = CreateDoc();
        var path = Path.Combine(Path.GetTempPath(), $"neoeditor-prev-{System.Guid.NewGuid():N}.png");
        using (var img = new SixLabors.ImageSharp.Image<Rgba32>(16, 16))
        {
            img.Mutate(ctx => ctx.BackgroundColor(new Rgba32(255, 0, 0, 255)));
            img.SaveAsPng(path);
        }

        try
        {
            var item = new PendingImageItem
            {
                Path = path,
                DisplayName = "prev.png",
                Kind = PendingImageKind.Imported
            };
            doc.PendingItems.Add(item);
            Assert.False(doc.HasPreview);

            // Selecting the item drives the right-side preview.
            doc.SelectedItem = item;
            Assert.True(doc.HasPreview);
            Assert.Equal("prev.png", doc.PreviewName);
            Assert.NotNull(doc.PreviewImage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SelectionChanges_NotifyCanOpenPending_ForButtonBinding()
    {
        // The Open button binds IsEnabled to CanOpenPending — the property must raise
        // PropertyChanged, otherwise the button stays disabled despite CanOpenPending
        // being true (command CanExecute notifications don't reach the binding).
        var doc = CreateDoc();
        var notifications = new List<string?>();
        doc.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        var item = new PendingImageItem
        {
            Path = Path.Combine(Path.GetTempPath(), "a.png"),
            DisplayName = "a.png",
            Kind = PendingImageKind.Imported
        };
        doc.PendingItems.Add(item);
        doc.SelectedItem = item;
        doc.SelectedItem = null;
        item.IsSelected = true;

        Assert.Contains(nameof(ImageCreateDocument.CanOpenPending), notifications);
    }

    [Fact]
    public void SelectionChanges_UpdateCanOpenPending()
    {
        var doc = CreateDoc();
        Assert.False(doc.CanOpenPending);

        var item = new PendingImageItem
        {
            Path = Path.Combine(Path.GetTempPath(), "a.png"),
            DisplayName = "a.png",
            Kind = PendingImageKind.Imported
        };
        doc.PendingItems.Add(item);
        Assert.False(doc.CanOpenPending);

        // Single-click selection enables Open without checking the box.
        doc.SelectedItem = item;
        Assert.True(doc.CanOpenPending);

        // Clearing the selection disables it again.
        doc.SelectedItem = null;
        Assert.False(doc.CanOpenPending);

        // Checking the box enables it independently of the selection.
        item.IsSelected = true;
        Assert.True(doc.CanOpenPending);
        Assert.Contains("(1)", doc.OpenPendingButtonText);
    }
}
