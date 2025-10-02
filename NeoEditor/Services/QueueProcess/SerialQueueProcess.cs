using System.Data;
using NeoEditor.Helpers;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.Services.QueueProcess;

public class SerialQueueProcess
    : QueueProcess<Tuple<DataSet, ModXmlData>, DataSet>
{
    private readonly Dictionary<string, SerialIdHelper> _serialIdHelpers = new();

    public SerialQueueProcess(TableConfig tableConfig, IEventAggregator eventAggregator)
        : base(eventAggregator)
    {
        foreach (var kv in tableConfig.GetTableKeys())
            _serialIdHelpers[kv.Key] = new SerialIdHelper(kv.Value);
        OnInit += () =>
        {
            Reset();
            return Task.CompletedTask;
        };
    }

    public void Reset()
    {
        foreach (var helper in _serialIdHelpers.Values) helper.Reset();
    }

    protected override Task<DataSet> Processor(Tuple<DataSet, ModXmlData> tp)
    {
        var ds = tp.Item1;
        var modData = tp.Item2;
        foreach (DataTable dt in ds.Tables)
            _serialIdHelpers[dt.TableName].SerialTable(dt, modData);
        return Task.FromResult(ds);
    }
}