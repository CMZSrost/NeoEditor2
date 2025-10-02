using System.Data;
using AutoMapper;
using NeoEditor.Data.Models.Dto;
using NeoEditor.Helpers;
using NeoEditor.Helpers.Converters;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.Services.QueueProcess;

public class LoadXmlQueueProcess(IEventAggregator eventAggregator, IMapper mapper)
    : QueueProcess<ModXmlData, Tuple<DataSet?, ModXmlData>>(eventAggregator)
{
    protected override async Task<Tuple<DataSet?, ModXmlData>> Processor(ModXmlData modData)
    {
        try
        {
            var ds = await GameXmlLoader.LoadXml(modData!.XmlPath!);
            foreach (DataTable dt in ds.Tables)
            {
                if (dt.TableName == "attackmodes")
                {
                    var res = DataTableToEntity<attackmode>.FillModel(dt);
                    break;
                }
            }
            return new Tuple<DataSet?, ModXmlData>(ds, modData);
        }
        catch (Exception ex)
        {
            return new Tuple<DataSet?, ModXmlData>(null, modData);
        }
    }
}