using HandyControl.Controls;
using NeoEditor.ViewModels.Controls.Tabs;

namespace NeoEditor.Views.Controls.Tabs;

public partial class AttackMode : TabItem
{
    public AttackMode(AttackModeViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}