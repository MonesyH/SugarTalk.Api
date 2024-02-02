using System.Threading;
using System.Threading.Tasks;
using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SugarTalk.Core.Services.Meetings;
using SugarTalk.Messages.Requests.Meetings;

namespace SugarTalk.Core.Handlers.RequestHandlers.Meetings;

public class GetAppointmentMeetingRequestHandler : IRequestHandler<GetAppointmentMeetingRequest, GetAppointmentMeetingResponse>
{
    private readonly IMeetingService _meetingService;

    public GetAppointmentMeetingRequestHandler(IMeetingService meetingService)
    {
        _meetingService = meetingService;
    }

    public async Task<GetAppointmentMeetingResponse> Handle(IReceiveContext<GetAppointmentMeetingRequest> context, CancellationToken cancellationToken)
    {
        return await _meetingService.GetAppointmentMeetingsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}