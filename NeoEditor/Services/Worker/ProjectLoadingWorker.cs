using System.Collections.Concurrent;
using System.Data;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Helpers.Converters;
using NeoEditor.Services.QueueProcess;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.Services.Worker;

public class ProjectLoadingWorker : ObservableRecipient
{
    private readonly ConcurrentQueue<DataSet> _dataSets = new();
    private readonly LoadXmlQueueProcess _loadXmlQueueProcess;
    private readonly SerialQueueProcess _serialQueueProcess;

    public ProjectLoadingWorker(SerialQueueProcess serialQueueProcess,
        LoadXmlQueueProcess loadXmlQueueProcess)
    {
        _serialQueueProcess = serialQueueProcess;
        _serialQueueProcess.OnResult += t => _dataSets.Enqueue(t);

        _loadXmlQueueProcess = loadXmlQueueProcess;
        _loadXmlQueueProcess.OnResult += t =>
        {
            if (t.Item1 != null) _serialQueueProcess.QueueIn.Enqueue(new Tuple<DataSet, ModXmlData>(t.Item1, t.Item2));
        };
    }

    public void Add(ModXmlData xmlData)
    {
        if (xmlData.XmlPath == null || !File.Exists(xmlData.XmlPath)) return;
        _loadXmlQueueProcess.QueueIn.Enqueue(xmlData);
    }

    public async Task RunAsync(string filePath, CancellationToken cancellationToken)
    {
        await _loadXmlQueueProcess.RunUtilEmpty(cancellationToken);
        await _serialQueueProcess.RunUtilEmpty(cancellationToken);
        await Task.Run(() =>
        {
            var dsTotal = new DataSet();
            while (_dataSets.TryDequeue(out var ds)) dsTotal.Merge(ds);
            ExcelConverter.DataSetToExcel(dsTotal, filePath);
        }, cancellationToken);
    }
}