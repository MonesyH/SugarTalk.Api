using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    
    //   public async Task<OpenAiUploadFileDto> UploadFileAsync(string purpose, string fileName, byte[] fileBytes, CancellationToken cancellationToken)
    // {
    // var purposeType = new Dictionary<string, string> { { "purpose", purpose } };
    // 
    // var file = new Dictionary<string, (byte[], string)> { { "file", (fileBytes, fileName) } };
    // 
    // return await _smartiesHttpClientFactory.PostAsMultipartAsync<OpenAiUploadFileDto>("https://api.openai.com/v1/files", 
    // purposeType, file, cancellationToken, headers: _openAiClientBuilder.GetRequestHeaders(OpenAiProvider.OpenAi)).ConfigureAwait(false);
    // }


    public async Task<string> CreateWhisperTranscriptionAsync(CreateWhisperTranscriptionRequestDto request, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string> { { "", "" } };
        
        var file = new Dictionary<string, (byte[], string)> { { "file", (request.File, request.FileName) } };
        
        return await _httpClientFactory.PostAsMultipartAsync<string>("https://api.openai.com/v1/audio/transcriptions", 
            parameters, file, cancellationToken).ConfigureAwait(false);
    }
}