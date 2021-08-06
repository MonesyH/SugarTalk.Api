using System.Threading.Tasks;
using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarTalk.Core.Entities;
using SugarTalk.Messages;
using SugarTalk.Messages.Commands;
using SugarTalk.Messages.Dtos.Meetings;
using SugarTalk.Messages.Requests.Meetings;

namespace SugarTalk.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class MeetingController: ControllerBase
    {
        private readonly IMediator _mediator;
        
        public MeetingController(IMediator mediator)
        {
            _mediator = mediator;
        }
        
        [Route("schedule"), HttpPost]
        public async Task<SugarTalkResponse<MeetingDto>> ScheduleMeeting(ScheduleMeetingCommand scheduleMeetingCommand)
        {
            return await _mediator.SendAsync<ScheduleMeetingCommand, SugarTalkResponse<MeetingDto>>(scheduleMeetingCommand);
        }
        
        [Route("session"), HttpGet]
        public async Task<SugarTalkResponse<MeetingSession>> GetMeetingSession([FromQuery] GetMeetingSessionRequest request)
        {
            return await _mediator.RequestAsync<GetMeetingSessionRequest, SugarTalkResponse<MeetingSession>>(request);
        }
    }
}