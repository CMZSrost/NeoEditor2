using AutoMapper;
using NeoEditor.Data.Models;
using SerialRecord = NeoEditor.Data.Models.Dto.SerialRecord;

namespace NeoEditor.Data;

public class SerialRecordProfile : Profile
{
    public SerialRecordProfile()
    {
        CreateMap<attackmode, SerialRecord>();

        CreateMap<battlemove, SerialRecord>();
        CreateMap<barterhex, SerialRecord>();

        CreateMap<camptype, SerialRecord>();
        CreateMap<chargeprofile, SerialRecord>();
        CreateMap<condition, SerialRecord>();
        CreateMap<containertype, SerialRecord>();
        CreateMap<creature, SerialRecord>();
        CreateMap<creaturesource, SerialRecord>();

        CreateMap<datafile, SerialRecord>();
        CreateMap<dmcplace, SerialRecord>();

        CreateMap<encounter, SerialRecord>();
        CreateMap<encountertrigger, SerialRecord>();

        CreateMap<faction, SerialRecord>();
        CreateMap<forbiddenhex, SerialRecord>();

        CreateMap<gamevar, SerialRecord>();

        CreateMap<headline, SerialRecord>();
        CreateMap<hextype, SerialRecord>();

        CreateMap<ingredient, SerialRecord>();
        CreateMap<itemprop, SerialRecord>();
        CreateMap<itemtype, SerialRecord>();
        CreateMap<image, SerialRecord>();

        CreateMap<map, SerialRecord>();

        CreateMap<recipe, SerialRecord>();

        CreateMap<treasuretable, SerialRecord>();
    }
}