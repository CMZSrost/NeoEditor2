using System.Collections.Concurrent;
using System.Data;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Services.QueueProcess;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.Services.Worker;

public class ProjectExportingWorker : ObservableRecipient
{
    private readonly ConcurrentQueue<DataSet> _dataSets = new();
    private readonly WriteXmlQueueProcess _writeXmlQueueProcess;

    public ProjectExportingWorker(WriteXmlQueueProcess writeXmlQueueProcess)
    {
        _writeXmlQueueProcess = writeXmlQueueProcess;
    }

    public void Add(DataSet ds, ModData modData)
    {
        if (modData.ModDirectoryPath == null || !Directory.Exists(modData.ModDirectoryPath)) return;
        _writeXmlQueueProcess.QueueIn.Enqueue(new Tuple<DataSet, ModData>(ds, modData));
    }

    public Task RunAsync(string filePath, CancellationToken cancellationToken)
    {
        return _writeXmlQueueProcess.RunUtilEmpty(cancellationToken);
    }
}