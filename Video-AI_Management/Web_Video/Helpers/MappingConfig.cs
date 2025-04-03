using AutoMapper;
using Database_Video.Entities;
using System;
using Web_Video.ViewModels.Admin;

namespace Web_Video.Helpers
{
    public class MappingConfig : Profile
    {
        public MappingConfig()
        {
            // From, to
            CreateMap<AppUser, UserDisplayGridViewModel>()
                .ForMember(d => d.ChannelId, opt => opt.MapFrom(s => s.Channel == null ? Guid.Empty : s.Channel.Id))
                .ForMember(d => d.ChannelName, opt => opt.MapFrom(s => s.Channel == null ? "" : s.Channel.ChannelName));
            
            CreateMap<AppUser, UserAddEditViewModel>();
        }
    }
}
