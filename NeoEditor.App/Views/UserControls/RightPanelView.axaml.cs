using NeoEditor.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.ImageTools.ViewModels;
using NeoEditor.ViewModels.MainContent;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls;

public partial class RightPanelView : UserControl
{
    private ImagePreviewContent? _imageContent;
    public ILocalizationService Loc => ViewServices.Loc;
    private ReferenceInspectorContent? _refContent;

    public RightPanelView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var messenger = WeakReferenceMessenger.Default;
        messenger.Register<VisualEditorRequestedMessage>(this, (_, m) =>
        {
            Dispatcher.UIThread.Post(() => EditorPanel.Show(m.EntityType, m.Entity));
        });

        var ws = ViewServices.Get<DocumentWorkspaceViewModel>();
        if (ws is not null)
        {
            _imageContent = ws.ImagePreview;
            _refContent = ws.ReferenceInspector;
            ImagePanel.DataContext = _imageContent;
            RefPanel.DataContext = _refContent;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
    }

}
