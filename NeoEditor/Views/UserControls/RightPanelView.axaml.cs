using NeoEditor.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class RightPanelView : UserControl
{
    private ImagePreviewContent? _imageContent;
    public LocalizationService Loc => App.Localizor;
    private ReferenceInspectorContent? _refContent;

    public RightPanelView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var messenger = App.ServiceProvider!.GetRequiredService<IMessenger>();
        messenger.Register<VisualEditorRequestedMessage>(this, (_, m) =>
        {
            Dispatcher.UIThread.Post(() => EditorPanel.Show(m.EntityType, m.Entity));
        });

        var ws = DocumentWorkspaceViewModel.Instance;
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
