using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SugarTalk.Core.Extensions;
using SugarTalk.Core.Ioc;
using SugarTalk.Messages.Dto.OpenAi;

namespace SugarTalk.Core.Services.Http.Clients;

public interface IOpenAiClient : IScopedDependency
{
    Task<string> CreateWhisperTranscriptionAsync(CreateWhisperTranscriptionRequestDto request, CancellationToken cancellationToken);
}

public class OpenAiClient : IOpenAiClient
{
    private readonly ISugarTalkHttpClientFactory _httpClientFactory;

    public OpenAiClient(ISugarTalkHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<string> CreateWhisperTranscriptionAsync(CreateWhisperTranscriptionRequestDto request, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "model", request.Model },
            { "language", request.Language.GetDescription()},
            { "response_format", request.ResponseFormat }
        };

        if (!string.IsNullOrEmpty(request.Prompt))
            parameters.Add("prompt", request.Prompt);
        
        var file = new Dictionary<string, (byte[], string)> { { "file", (request.File, request.FileName) } };
        
        return await _httpClientFactory.PostAsMultipartAsync<string>("https://api.openai.com/v1/audio/transcriptions", 
            parameters, file, cancellationToken).ConfigureAwait(false);
    }
}