using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Options;
using NeoEditor.ViewModels;
using unvell.ReoGrid;

namespace NeoEditor.Views;

public partial class EditTableReoPage : UserControl
{
    private readonly IEventAggregator _eventAggregator;

    public EditTableReoPage(IContainer container, IOptions<ProjectOption> option, IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        DataContext = container.GetService<EditTableViewModel>();
        ExcelPath = option.Value.EditExcelName;
        InitializeComponent();
        ReoGrid.Worksheets.Clear();
        Subscribe();
    }

    public string? ExcelPath { get; set; }

    private void Subscribe()
    {
        _eventAggregator.GetEvent<LoadFromXlsxEvent>().Subscribe(LoadFromXls, ThreadOption.UIThread);
    }

    private void LoadFromXls(LoadFromXlsxMessage message)
    {
        Console.WriteLine("Load From xlsx!");
        if (Name != message.TargetTable) return;
        Console.WriteLine("Entry From xlsx!");
        ReoGrid.Worksheets.Clear();
        ReoGrid.Load(message.FilePath);

        foreach (var reoGridWorksheet in ReoGrid.Worksheets)
        {
            var modIndexColumn = -1;
            reoGridWorksheet.RowHeaders[0].Style.Bold = true;
            for (var i = 1; i <= reoGridWorksheet.MaxContentCol; i++)
            {
                if (reoGridWorksheet.Cells[0, i].DisplayText == "modIndex")
                    modIndexColumn = i;
                reoGridWorksheet.Cells[0, i].IsReadOnly = true;
            }

            reoGridWorksheet.FreezeToCell(1, 0);

            if (reoGridWorksheet.Name == "gamevars")
                reoGridWorksheet.SetSettings(WorksheetSettings.Edit_Readonly, true);

            reoGridWorksheet.Resize(reoGridWorksheet.MaxContentRow, reoGridWorksheet.MaxContentCol);

            if (modIndexColumn > 0) // 将游戏原版数据设为不可改
                reoGridWorksheet.IterateCells(1, 0, reoGridWorksheet.MaxContentRow, reoGridWorksheet.MaxContentCol,
                    true, (i, j, c) =>
                    {
                        if (reoGridWorksheet.Cells[c.Row, modIndexColumn].DisplayText == "-1") c.IsReadOnly = true;
                        return true;
                    });
        }
    }
}