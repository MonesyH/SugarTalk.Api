using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Mediator.Net;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using SugarTalk.Core.Data;
using SugarTalk.Core.Domain.Meeting;
using SugarTalk.Core.Services.AntMediaServer;
using SugarTalk.Core.Services.Exceptions;
using SugarTalk.Core.Services.Identity;
using SugarTalk.IntegrationTests.TestBaseClasses;
using SugarTalk.IntegrationTests.Utils.Account;
using SugarTalk.IntegrationTests.Utils.Meetings;
using SugarTalk.Messages.Commands.Meetings;
using SugarTalk.Messages.Dto.Meetings;
using SugarTalk.Messages.Enums.Meeting;
using SugarTalk.Messages.Requests.Meetings;
using Xunit;

namespace SugarTalk.IntegrationTests.Services.Meeting;

public class MeetingServiceFixture : MeetingFixtureBase
{
    private readonly MeetingUtil _meetingUtil;
    private readonly AccountUtil _accountUtil;

    public MeetingServiceFixture()
    {
        _meetingUtil = new MeetingUtil(CurrentScope);
        _accountUtil = new AccountUtil(CurrentScope);
    }

    [Fact]
    public async Task ShouldScheduleMeeting()
    {
        var response = await _meetingUtil.ScheduleMeeting();

        response.Data.ShouldNotBeNull();
        response.Data.MeetingStreamMode.ShouldBe(MeetingStreamMode.MCU);
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

            response.Data.Meeting.MeetingNumber.ShouldBe(meetingResult.MeetingNumber);
            response.Data.Meeting.MeetingStreamMode.ShouldBe(MeetingStreamMode.MCU);
            response.Data.Meeting.Id.ShouldBe(meetingResult.Id);
        }, builder =>
        {
            var antMediaServerUtilService = Substitute.For<IAntMediaServerUtilService>();

            antMediaServerUtilService.AddStreamToMeetingAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<string>(), CancellationToken.None)
                .Returns(new ConferenceRoomResponseBaseDto { Success = true });

            builder.RegisterInstance(antMediaServerUtilService);
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
        }, builder =>
        {
            var antMediaServerUtilService = Substitute.For<IAntMediaServerUtilService>();

            antMediaServerUtilService.RemoveStreamFromMeetingAsync(Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<string>(), CancellationToken.None)
                .Returns(new ConferenceRoomResponseBaseDto { Success = true });

            builder.RegisterInstance(antMediaServerUtilService);
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

    [Fact]
    public async Task CanEndMeeting()
    {
        var scheduleMeetingResponse = await _meetingUtil.ScheduleMeeting();

        var meeting = await _meetingUtil.GetMeeting(scheduleMeetingResponse.Data.MeetingNumber);

        await _meetingUtil.JoinMeeting(meeting.MeetingNumber);
        
        await Run<IMediator, IRepository>(async (mediator, repository) =>
        {
            var beforeUserSession = await repository.QueryNoTracking<MeetingUserSession>()
                .Where(x => x.MeetingId == meeting.Id).ToListAsync();

            beforeUserSession.Count.ShouldBe(1);

            await mediator.SendAsync<EndMeetingCommand, EndMeetingResponse>(new EndMeetingCommand
            {
                MeetingNumber = meeting.MeetingNumber
            });

            var afterMeetings = await repository.QueryNoTracking<Core.Domain.Meeting.Meeting>().ToListAsync();
            
            var afterUserSession = await repository.QueryNoTracking<MeetingUserSession>()
                .Where(x => x.MeetingId == meeting.Id).ToListAsync();

            afterMeetings.Count.ShouldBe(0);
            afterUserSession.Count.ShouldBe(0);
        }, builder =>
        {
            var antMediaServerUtilService = Substitute.For<IAntMediaServerUtilService>();

            antMediaServerUtilService
                .RemoveMeetingByMeetingNumberAsync(Arg.Any<string>(), Arg.Any<string>(), CancellationToken.None)
                .Returns(new ConferenceRoomResponseBaseDto { Success = true });

            builder.RegisterInstance(antMediaServerUtilService);
        });
    }
    
    [Fact]
    public async Task CanGetMeetingByNumber()
    {
        var scheduleMeetingResponse = await _meetingUtil.ScheduleMeeting();

        var user1 = await _accountUtil.AddUserAccount("mars", "123");
        var user2 = await _accountUtil.AddUserAccount("greg", "123");

        await _meetingUtil.JoinMeeting(scheduleMeetingResponse.Data.MeetingNumber);

        await Run<IMediator, IRepository, IUnitOfWork>(async (mediator, repository, unitOfWork) =>
        {
            await repository.InsertAllAsync(new List<MeetingUserSession>
            {
                new()
                {
                    UserId = user1.Id,
                    IsMuted = false,
                    MeetingId = scheduleMeetingResponse.Data.Id
                },
                new()
                {
                    UserId = user2.Id,
                    IsMuted = true,
                    MeetingId = scheduleMeetingResponse.Data.Id
                }
            });

            await unitOfWork.SaveChangesAsync().ConfigureAwait(false);

            var response = await mediator.RequestAsync<GetMeetingByNumberRequest, GetMeetingByNumberResponse>(
                new GetMeetingByNumberRequest
                {
                    MeetingNumber = scheduleMeetingResponse.Data.MeetingNumber
                });

            response.Data.ShouldNotBeNull();
            response.Data.UserSessions.Count.ShouldBe(3);
            response.Data.MeetingStreamMode.ShouldBe(MeetingStreamMode.MCU);
            response.Data.MeetingNumber.ShouldBe(scheduleMeetingResponse.Data.MeetingNumber);
            response.Data.UserSessions.Single(x => x.UserId == 1).UserName.ShouldBe("TEST_USER");
            response.Data.UserSessions.Single(x => x.UserId == user1.Id).UserName.ShouldBe("mars");
            response.Data.UserSessions.Single(x => x.UserId == user2.Id).UserName.ShouldBe("greg");
        });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CanShareScreen(bool isSharingScreen, bool expect)
    {
        var scheduleMeetingResponse = await _meetingUtil.ScheduleMeeting();

        var meeting = await _meetingUtil.GetMeeting(scheduleMeetingResponse.Data.MeetingNumber);

        var user = await _accountUtil.AddUserAccount("test", "123");

        await _meetingUtil.AddMeetingUserSession(1, meeting.Id, 1);
        await _meetingUtil.AddMeetingUserSession(2, meeting.Id, user.Id, isSharingScreen: isSharingScreen);
        
        await Run<IMediator>(async mediator =>
        {
            var response = await mediator.SendAsync<ShareScreenCommand, ShareScreenResponse>(
                new ShareScreenCommand
                {
                    MeetingUserSessionId = 1,
                    StreamId = "123456",
                    IsShared = true
                });
            
            response.Data.MeetingUserSession.IsSharingScreen.ShouldBe(expect);
        });
    }

    [Fact]
    public async Task ShouldExceptionWhenChangeAudio()
    {
        var scheduleMeetingResponse = await _meetingUtil.ScheduleMeeting();

        var meeting = await _meetingUtil.GetMeeting(scheduleMeetingResponse.Data.MeetingNumber);

        var user1 = await _accountUtil.AddUserAccount("test1", "123");
        var user2 = await _accountUtil.AddUserAccount("test2", "123");

        await Assert.ThrowsAsync<CannotChangeAudioWhenConfirmRequiredException>(async () =>
        {
            await Run<IMediator>(async (mediator) =>
            {
                await _meetingUtil.AddMeetingUserSession(1, meeting.Id, user1.Id);
                await _meetingUtil.AddMeetingUserSession(2, meeting.Id, user2.Id);
                
                await mediator.SendAsync<ChangeAudioCommand, ChangeAudioResponse>(
                    new ChangeAudioCommand
                    {
                        MeetingUserSessionId = 1,
                        StreamId = "123456",
                        IsMuted = true
                    });
            });
        });
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task CanChangeAudio(bool isMuted, bool expect)
    {
        var scheduleMeetingResponse = await _meetingUtil.ScheduleMeeting();

        var meeting = await _meetingUtil.GetMeeting(scheduleMeetingResponse.Data.MeetingNumber);

        var user1 = await _accountUtil.AddUserAccount("test1", "123");
        var user2 = await _accountUtil.AddUserAccount("test2", "123");

        await Run<IMediator, ICurrentUser>(async (mediator, currentUser) =>
        {
            await _meetingUtil.AddMeetingUserSession(2, meeting.Id, user2.Id);
            await _meetingUtil.AddMeetingUserSession(1, meeting.Id, currentUser.Id);
            
            var response = await mediator.SendAsync<ChangeAudioCommand, ChangeAudioResponse>(
                new ChangeAudioCommand
                {
                    MeetingUserSessionId = 1,
                    StreamId = "123456",
                    IsMuted = isMuted
                });

            response.Data.MeetingUserSession.IsMuted.ShouldBe(expect);
        }, builder =>
        {
            var antMediaServerUtilService = Substitute.For<IAntMediaServerUtilService>();

            antMediaServerUtilService.AddStreamToMeetingAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), CancellationToken.None)
                .Returns(new ConferenceRoomResponseBaseDto { Success = true });

            builder.RegisterInstance(antMediaServerUtilService);
        });
    }
}
