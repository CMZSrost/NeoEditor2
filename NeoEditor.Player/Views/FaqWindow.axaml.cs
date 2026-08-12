using Avalonia.Controls;

namespace NeoEditor.Player.Views;

/// <summary>
/// 玩家向 FAQ（v2.79）：常见问题 Q&A，内容来自 resx（Help.Faq.Q1..Q7 / A1..A7），
/// 绑定 LocalizationManager 随语言切换即时刷新。
/// </summary>
public partial class FaqWindow : Window
{
    public FaqWindow()
    {
        InitializeComponent();
    }
}
