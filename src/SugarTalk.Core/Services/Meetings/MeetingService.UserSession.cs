using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SugarTalk.Core.Domain.Meeting;
using SugarTalk.Core.Services.Exceptions;
using SugarTalk.Messages.Commands.Meetings;
using SugarTalk.Messages.Enums.Meeting;
using SugarTalk.Messages.Events.Meeting;

namespace SugarTalk.Core.Services.Meetings;

public partial interface IMeetingService
{
    Task<AudioChangedEvent> ChangeAudioAsync(
        ChangeAudioCommand command, CancellationToken cancellationToken);

    Task<ScreenSharedEvent> ShareScreenAsync(
        ShareScreenCommand command, CancellationToken cancellationToken);
}

public partial class MeetingService
{
    public async Task<AudioChangedEvent> ChangeAudioAsync(ChangeAudioCommand command, CancellationToken cancellationToken)
    {
        var userSession = await _meetingDataProvider
            .GetMeetingUserSessionByIdAsync(command.MeetingUserSessionId, cancellationToken).ConfigureAwait(false);

        var meeting = await _meetingDataProvider.GetMeetingByIdAsync(userSession.MeetingId, cancellationToken).ConfigureAwait(false);

        if (meeting == null) throw new MeetingNotFoundException();

        if (command.IsMuted && userSession.UserId != _currentUser.Id) throw new CannotChangeAudioWhenConfirmRequiredException();

        userSession.IsMuted = command.IsMuted;

        await _meetingDataProvider.UpdateMeetingUserSessionAsync(userSession, cancellationToken).ConfigureAwait(false);

        var updateMeeting = await _meetingDataProvider.GetMeetingAsync(meeting.MeetingNumber, cancellationToken).ConfigureAwait(false);

        return new AudioChangedEvent
        {
            MeetingUserSession = updateMeeting.UserSessions.FirstOrDefault(x => x.Id == userSession.Id)
        };
    }

    public async Task<ScreenSharedEvent> ShareScreenAsync(ShareScreenCommand command, CancellationToken cancellationToken)
    {
        var userSession = await _meetingDataProvider
            .GetMeetingUserSessionByIdAsync(command.MeetingUserSessionId, cancellationToken).ConfigureAwait(false);

        var meeting = await _meetingDataProvider.GetMeetingByIdAsync(userSession.MeetingId, cancellationToken).ConfigureAwait(false);
        
        if (meeting == null) throw new MeetingNotFoundException();

        if (command.IsShared)
        {
            var otherSharing = await IsOtherSharingAsync(userSession, meeting, cancellationToken).ConfigureAwait(false);

            if (!otherSharing && userSession.UserId == _currentUser.Id)
            {
                userSession.IsSharingScreen = true;
                
                await AddMeetingUserSessionStreamAsync(
                    userSession.Id, command.StreamId, MeetingStreamType.ScreenSharing, cancellationToken).ConfigureAwait(false);
            }
            else
                throw new CannotSharingScreenWhenSharingException();
        }
        else
        {
            userSession.IsSharingScreen = false;

            await RemoveMeetingUserSessionStreamAsync(userSession.Id, MeetingStreamType.ScreenSharing, cancellationToken).ConfigureAwait(false);
        }

        await _meetingDataProvider
            .UpdateMeetingUserSessionAsync(userSession, cancellationToken).ConfigureAwait(false);
        
        var updateMeeting = await _meetingDataProvider.GetMeetingAsync(meeting.MeetingNumber, cancellationToken).ConfigureAwait(false);

        return new ScreenSharedEvent
        {
            MeetingUserSession = updateMeeting.UserSessions.FirstOrDefault(x => x.Id == userSession.Id)
        };
    }
    
    private async Task AddMeetingUserSessionStreamAsync(
        int userSessionId, string streamId, MeetingStreamType streamType, CancellationToken cancellationToken)
    {
        var userSessionStream = new MeetingUserSessionStream
        {
            StreamId = streamId,
            StreamType = streamType,
            MeetingUserSessionId = userSessionId
        };

        var userSessionStreams = 
            await _meetingDataProvider.GetMeetingUserSessionStreamsAsync(userSessionId, cancellationToken).ConfigureAwait(false);

        if (userSessionStreams.Any(x => x.StreamType == streamType))
            throw new CannotAddStreamWhenStreamTypeExistException(streamType);
        
        await _meetingDataProvider
            .AddMeetingUserSessionStreamAsync(userSessionStream, cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveMeetingUserSessionStreamAsync(
        int userSessionId, MeetingStreamType streamType, CancellationToken cancellationToken)
    {
        var userSessionStreams = await _meetingDataProvider
            .GetMeetingUserSessionStreamsAsync(userSessionId, cancellationToken).ConfigureAwait(false);

        await _meetingDataProvider.RemoveMeetingUserSessionStreamsAsync(
            userSessionStreams.Where(x => x.StreamType == streamType).ToList(), cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<bool> IsOtherSharingAsync(MeetingUserSession userSession, Meeting meeting, CancellationToken cancellationToken)
    {
        var getResponse = await _antMediaServerUtilService
            .GetMeetingByMeetingNumberAsync(appName, meeting.MeetingNumber, cancellationToken).ConfigureAwait(false);

        if (getResponse is null) throw new MeetingNotFoundException();

        var sharingUserSession = await _meetingDataProvider
            .GetSharingUserSessionAsync(meeting.Id, userSession.UserId, cancellationToken).ConfigureAwait(false);

        if (sharingUserSession is null) return false;
        
        var sharingUserSessionStreams = await _meetingDataProvider
            .GetMeetingUserSessionStreamsAsync(sharingUserSession.Id, cancellationToken).ConfigureAwait(false);

        if (!getResponse.RoomStreamList.Any(x => sharingUserSessionStreams.Select(x => x.StreamId).Contains(x)))
        {
            sharingUserSession.IsSharingScreen = false;

            await _meetingDataProvider
                .RemoveMeetingUserSessionStreamsAsync(sharingUserSessionStreams, cancellationToken).ConfigureAwait(false);
            
            await _meetingDataProvider
                .RemoveMeetingUserSessionsAsync(new List<MeetingUserSession> { sharingUserSession }, cancellationToken).ConfigureAwait(false);

            return false;
        }
        
        return true;
    }
}