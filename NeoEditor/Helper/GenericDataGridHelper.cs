using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Microsoft.EntityFrameworkCore;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace NeoEditor.Helper
{
    public static class GenericDataGridHelper
    {
        public static void ConfigureColumn<T>(DataGridAutoGeneratingColumnEventArgs e, Func<string, string> localizer)
        {
            ConfigureColumn(e, localizer, typeof(T));
        }

        /// <summary>
        /// 根据模型上的特性配置自动生成的列
        /// </summary>
        /// <typeparam name="T">模型类型</typeparam>
        /// <param name="e">事件参数</param>
        /// <param name="localizer">本地化函数：接收资源键，返回本地化字符串</param>
        public static void ConfigureColumn(DataGridAutoGeneratingColumnEventArgs e, Func<string, string> localizer,
            Type modelType)
        {
            var property = modelType.GetProperty(e.PropertyName);
            if (property == null) return;

            // 1. 如果没有 [Column] 特性，则不生成该列（视为内部字段）
            var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
            if (columnAttr == null)
            {
                e.Cancel = true;
                return;
            }

            string headerText = property.Name; // 默认用属性名

            // 2. 获取 [Display] 特性，用于工具提示
            var displayAttr = property.GetCustomAttribute<DisplayAttribute>();
            string comment = displayAttr?.Name ?? ""; // 默认工具提示为空
            if (displayAttr != null && !string.IsNullOrEmpty(displayAttr.Name))
            {
                comment = localizer(displayAttr.Name);
            }

            // 4. 构建自定义列头（包含文本和可选的工具提示）
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4};
            var headerTextBlock = new TextBlock { Text = headerText, VerticalAlignment = VerticalAlignment.Center };
            headerPanel.Children.Add(headerTextBlock);
            
            if (!string.IsNullOrEmpty(comment))
            {
                // 为文本块附加工具提示，也可以添加一个信息图标
                ToolTip.SetTip(headerPanel, comment);
                // 可选：添加一个信息图标（需引入 FluentIcons 或其它图标库）
                // var icon = new PathIcon { Data = (StreamGeometry)Application.Current.FindResource("InfoIcon") };
                // ToolTip.SetTip(icon, comment);
                // headerPanel.Children.Add(icon);
            }

            // 5. 根据属性类型或 Column.TypeName 决定是否替换为自定义模板列
            //    如果列已经是 DataGridTextColumn 且属性为数值类型，可设置格式字符串
            if (e.Column is DataGridTextColumn textColumn)
            {
                // 数值类型设置默认格式
                if (property.PropertyType == typeof(double) || property.PropertyType == typeof(float) ||
                    property.PropertyType == typeof(int))
                {
                    // 保留两位小数（可根据需要调整）
                    textColumn.Binding = new Binding(property.Name) { StringFormat = "0.##" };
                }
            }

            // 如果 TypeName 包含 "longtext"，则替换为多行文本编辑列
            if (columnAttr.TypeName != null &&
                columnAttr.TypeName.Contains("longtext", StringComparison.OrdinalIgnoreCase))
            {
                var templateColumn = new DataGridTemplateColumn
                {
                    Header = headerPanel,
                    // 只读视图：简单显示文本
                    CellTemplate = new FuncDataTemplate<object>((item, _) =>
                    {
                        var value = property.GetValue(item);
                        return new TextBlock
                        {
                            Text = value?.ToString(),
                            TextWrapping = TextWrapping.Wrap,
                            MaxHeight = 60
                        };
                    }),
                    // 编辑视图：多行文本框
                    CellEditingTemplate = new FuncDataTemplate<object>((item, _) =>
                    {
                        var textBox = new TextBox
                        {
                            AcceptsReturn = true,
                            TextWrapping = TextWrapping.Wrap,
                            MaxHeight = 120
                        };
                        textBox.Bind(TextBox.TextProperty, new Binding(property.Name));
                        return textBox;
                    })
                };
                e.Column = templateColumn;
                return;
            }

            // 其他情况：保留原列类型，仅设置 Header
            e.Column.Header = headerPanel;

            // 若列支持只读属性，可以根据需要设置（例如根据属性的 setter 是否存在）
            // e.Column.IsReadOnly = !property.CanWrite;
            
            
        }
    }
}