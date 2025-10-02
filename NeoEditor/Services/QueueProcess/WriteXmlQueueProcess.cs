using System.Data;
using NeoEditor.Helpers;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.Services.QueueProcess;

public class WriteXmlQueueProcess(IEventAggregator eventAggregator)
    : QueueProcess<Tuple<DataSet, ModData>, bool>(eventAggregator)
{
    protected override async Task<bool> Processor(Tuple<DataSet, ModData> tp)
    {
        var ds = tp.Item1;
        var modData = tp.Item2;

        if (modData.ModDirectoryPath == null)
            return false;

        try
        {
            await GameXmlWriter.WriteXml(ds, modData.ModDirectoryPath);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
}