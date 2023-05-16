using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mediator.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SugarTalk.Core.Data;
using SugarTalk.Core.Domain.Meeting;
using SugarTalk.IntegrationTests.TestBaseClasses;
using SugarTalk.IntegrationTests.Utils.Meetings;
using SugarTalk.Messages.Commands.Meetings;
using SugarTalk.Messages.Enums.Meeting;
using Xunit;

namespace SugarTalk.IntegrationTests.Services.Meeting;

public class MeetingServiceFixture : MeetingFixtureBase
{
    private readonly MeetingUtil _meetingUtil;

    public MeetingServiceFixture()
    {
        _meetingUtil = new MeetingUtil(CurrentScope);
    }

    [Fact]
    public async Task ShouldScheduleMeeting()
    {
        var response = await _meetingUtil.ScheduleMeeting();

        response.Data.ShouldNotBeNull();
        response.Data.Mode.ShouldBe("mcu");
    }

    [Fact]
    public async Task ShouldGetMeeting()
    {
        var meetingId = Guid.NewGuid();

        await _meetingUtil.AddMeeting(meetingId, "123");

        await Run<IRepository>(async repository =>
        {
            var response = await repository
                .Query<Core.Domain.Meeting.Meeting>(x => x.Id == meetingId)
                .SingleAsync(CancellationToken.None);

            response.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task CanJoinMeeting()
    {
        var scheduleMeetingResponse = await _meetingUtil.ScheduleMeeting();

        await Run<IMediator, IRepository>(async (mediator, repository) =>
        {
            var response = await mediator.SendAsync<JoinMeetingCommand, JoinMeetingResponse>(new JoinMeetingCommand
            {
                MeetingNumber = scheduleMeetingResponse.Data.MeetingNumber,
                IsMuted = false
            });

            var meetingResult = await repository.Query<Core.Domain.Meeting.Meeting>()
                .Where(x => x.MeetingNumber == scheduleMeetingResponse.Data.MeetingNumber)
                .SingleAsync(CancellationToken.None);

            response.Data.MeetingNumber.ShouldBe(meetingResult.MeetingNumber);
            response.Data.MeetingStreamMode.ShouldBe(MeetingStreamMode.MCU);
            response.Data.Id.ShouldBe(meetingResult.Id);
        });
    }

    [Fact]
    public async Task CanOutMeeting()
    {
        var scheduleMeetingResponse = await _meetingUtil.ScheduleMeeting();

        var meeting = await _meetingUtil.GetMeeting(scheduleMeetingResponse.Data.MeetingNumber);

        await _meetingUtil.JoinMeeting(meeting.MeetingNumber);

        await Run<IMediator, IRepository>(async (mediator, repository) =>
        {
            var beforeUserSession = await repository.QueryNoTracking<MeetingUserSession>()
                .Where(x => x.MeetingId == meeting.Id).ToListAsync();

            beforeUserSession.Count.ShouldBe(1);

            await mediator.SendAsync<OutMeetingCommand, OutMeetingResponse>(new OutMeetingCommand
            {
                MeetingId = meeting.Id
            });

            var afterUserSession = await repository.QueryNoTracking<MeetingUserSession>()
                .Where(x => x.MeetingId == meeting.Id).ToListAsync();

            afterUserSession.Count.ShouldBe(0);
        });
    }

    [Fact]
    public async Task ShouldNotThrowWhenJoinMeetingDuplicated()
    {
        var isNotThrow = true;
        
        try
        {
            var scheduleMeetingResponse = await _meetingUtil.ScheduleMeeting();

            var meeting = await _meetingUtil.GetMeeting(scheduleMeetingResponse.Data.MeetingNumber);

            await _meetingUtil.JoinMeeting(meeting.MeetingNumber);
            await _meetingUtil.JoinMeeting(meeting.MeetingNumber);
        }
        catch (Exception ex)
        {
            isNotThrow = false;
        }
        
        isNotThrow.ShouldBeTrue();
    }
}
