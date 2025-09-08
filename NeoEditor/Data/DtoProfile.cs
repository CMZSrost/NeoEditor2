using AutoMapper;
using NeoEditor.Data.Models;
using Dto = NeoEditor.Data.Models.Dto;

namespace NeoEditor.Data;

public class DtoProfile : Profile
{
    public DtoProfile()
    {
        CreateMap<attackmode, Dto.attackmode>().ReverseMap();
        CreateMap<battlemove, Dto.battlemove>().ReverseMap();
        CreateMap<barterhex, Dto.barterhex>().ReverseMap();
        CreateMap<camptype, Dto.camptype>().ReverseMap();
        CreateMap<chargeprofile, Dto.chargeprofile>().ReverseMap();
        CreateMap<condition, Dto.condition>().ReverseMap();
        CreateMap<containertype, Dto.containertype>().ReverseMap();
        CreateMap<creature, Dto.creature>().ReverseMap();
        CreateMap<creaturesource, Dto.creaturesource>().ReverseMap();
        CreateMap<datafile, Dto.datafile>().ReverseMap();
        CreateMap<dmcplace, Dto.dmcplace>().ReverseMap();
        CreateMap<encounter, Dto.encounter>().ReverseMap();
        CreateMap<encountertrigger, Dto.encountertrigger>().ReverseMap();
        CreateMap<faction, Dto.faction>().ReverseMap();
        CreateMap<forbiddenhex, Dto.forbiddenhex>().ReverseMap();
        CreateMap<gamevar, Dto.gamevar>().ReverseMap();
        CreateMap<headline, Dto.headline>().ReverseMap();
        CreateMap<hextype, Dto.hextype>().ReverseMap();
        CreateMap<ingredient, Dto.ingredient>().ReverseMap();
        CreateMap<itemprop, Dto.itemprop>().ReverseMap();
        CreateMap<itemtype, Dto.itemtype>().ReverseMap();
        CreateMap<image, Dto.image>().ReverseMap();
        CreateMap<map, Dto.map>().ReverseMap();
        CreateMap<recipe, Dto.recipe>().ReverseMap();
        CreateMap<treasuretable, Dto.treasuretable>().ReverseMap();
    }
}