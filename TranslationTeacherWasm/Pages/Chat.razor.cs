 using System.Text.Json;
 using KristofferStrube.Blazor.DOM;
using KristofferStrube.Blazor.FileAPI;
using KristofferStrube.Blazor.MediaCaptureStreams;
using KristofferStrube.Blazor.MediaStreamRecording;
using KristofferStrube.Blazor.WebAudio;
using KristofferStrube.Blazor.WebIDL.Exceptions;
using Microsoft.AspNetCore.Components;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Audio;
using TranslationTeacherWasm.Models;
using TranslationTeacherWasm.Shared;

namespace TranslationTeacherWasm.Pages;

public partial class Chat
{
    [Inject(Key = "npc")]
    private IChatCompletionService NpcChatCompletionService { get; set; }
    [Inject(Key = "translator")]
    private IChatCompletionService TranslatorChatCompletionService { get; set; }
    
    private string status = "Idle";
    private string spokenAudioTranscribed = null;
    private string userTranslatedToEnglish;
    private string npcTranslatedToLanguage;
    private string responseText = null;
    private string selectedLanguage = "es,spanish";
    private bool generateSpeech = true;
    private AudioBuffer npcSpokenResponse;
    private ChatHistory ChatHistory = new ChatHistory();
    private AudioBufferSourceNode currentAudioBufferNode = default!;
    
    private string? error;
    private MediaStream? mediaStream;
    private readonly List<(string label, string id)> audioOptions = [];
    private string? selectedAudioSource;
    private MediaStreamAudioSourceNode? liveSourceNoce;
    private AnalyserNode? liveAnalyzer;

    private MediaRecorder? recorder;
    private EventListener<BlobEvent>? dataAvailableEventListener;
    private byte[] spokenAudio;
    private AudioBuffer? audioBuffer;
    private readonly List<Blob> blobsRecorded = [];
    private AudioContext? context;
    private AudioBufferSourceNode? audioSourceNode;
    private AnalyserNode? bufferAnalyzer;

    private AmplitudePlot plot = default!;

    private async Task OpenAudioStream()
    {
        try
        {   
            var mediaTrackConstraints = new MediaTrackConstraints
            {
                EchoCancellation = true,
                NoiseSuppression = true,
                AutoGainControl = false,
                DeviceId = selectedAudioSource is null ? null : new ConstrainDomString(selectedAudioSource)
            };

            MediaDevices mediaDevices = await MediaDevicesService.GetMediaDevicesAsync();
            mediaStream = await mediaDevices.GetUserMediaAsync(new MediaStreamConstraints() { Audio = mediaTrackConstraints });

            MediaDeviceInfo[] deviceInfos = await mediaDevices.EnumerateDevicesAsync();
            audioOptions.Clear();
            foreach (MediaDeviceInfo device in deviceInfos)
            {
                if (await device.GetKindAsync() is MediaDeviceKind.AudioInput)
                {
                    audioOptions.Add((await device.GetLabelAsync(), await device.GetDeviceIdAsync()));
                }
            }

            context = await AudioContext.CreateAsync(JSRuntime);

            MediaStreamAudioSourceOptions options = new()
            {
                MediaStream = mediaStream
            };
            liveSourceNoce = await context.CreateMediaStreamSourceAsync(mediaStream);
            liveAnalyzer = await AnalyserNode.CreateAsync(JSRuntime, context);
            await liveSourceNoce.ConnectAsync(liveAnalyzer);

            await StartRecording();
        }
        catch (WebIDLException ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private async Task StartRecording()
    {
        if (mediaStream is null)
        {
            return;
        }

        status = "Listening";

        try
        {
            recorder = await MediaRecorder.CreateAsync(JSRuntime, mediaStream);

            dataAvailableEventListener = await EventListener<BlobEvent>.CreateAsync(JSRuntime, async (BlobEvent e) =>
            {
                Blob blob = await e.GetDataAsync();
                blobsRecorded.Add(blob);
            });
            await recorder.AddOnDataAvailableEventListenerAsync(dataAvailableEventListener);

            await recorder.StartAsync();
        }
        catch (WebIDLException ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private async Task StopRecording()
    {
        if (mediaStream is null || recorder is null || dataAvailableEventListener is null || context is null)
        {
            return;
        }

        try
        {
            // Event listener for when it actually stopped.
            EventListener<Event> stopEventListener = default!;
            stopEventListener = await EventListener<Event>.CreateAsync(JSRuntime, async _ =>
            {
                try
                {
                    spokenAudioTranscribed = "";
                    userTranslatedToEnglish = "";
                    responseText = "";
                    npcTranslatedToLanguage = "";
                    
                    MediaStreamTrack[] audioTracks = await mediaStream.GetAudioTracksAsync();
                    foreach (MediaStreamTrack track in audioTracks)
                    {
                        await track.StopAsync();
                        await track.DisposeAsync();
                    }

                    await recorder.RemoveOnStopEventListenerAsync(stopEventListener);
                    await stopEventListener.DisposeAsync();

                    await recorder.RemoveOnDataAvailableEventListenerAsync(dataAvailableEventListener);
                    await dataAvailableEventListener.DisposeAsync();
                    await recorder.DisposeAsync();

                    await using Blob combinedBlob = await Blob.CreateAsync(JSRuntime, [.. blobsRecorded],
                        new() { Type = await blobsRecorded.First().GetTypeAsync() });

                    foreach (Blob blob in blobsRecorded)
                    {
                        await blob.DisposeAsync();
                    }

                    spokenAudio = await combinedBlob.ArrayBufferAsync();
                    audioBuffer = await context.DecodeAudioDataAsync(spokenAudio);

                    status = "Transcribing...";

                    StateHasChanged();

                    var transcription = (await WhisperApiClient.TranscribeAsync(
                        spokenAudio, language: selectedLanguage.Substring(0, 2), output: "json"));
                    spokenAudioTranscribed = JsonSerializer.Deserialize<TranscriptionResult>(transcription).Text;

                    status = "Translating...";

                    StateHasChanged();

                    var responseTranslation = await TranslatorChatCompletionService
                        .GetChatMessageContentAsync(
                            $"""
                             Analyze the following {selectedLanguage.Substring(3)} segment surrounded in triple backticks and generate a single refined english translation. Only output the refined translation, do not explain.
                             The {selectedLanguage.Substring(3)} segment:
                             ```{spokenAudioTranscribed}```
                             """);
                    userTranslatedToEnglish = responseTranslation.Content;

                    status = "Thinking...";

                    StateHasChanged();

                    ChatHistory.AddUserMessage(userTranslatedToEnglish);

                    var responseNpc = await NpcChatCompletionService
                        .GetChatMessageContentAsync(ChatHistory, new OpenAIPromptExecutionSettings()
                        {
                            ChatSystemPrompt = Configuration["npcPrompt"] ?? $"""
                                                                              /no_think
                                                                              You are a woman that works in a library. You are an expert librarian.
                                                                              You are responding to the user's message in the first person.
                                                                              Your response should be in english.
                                                                              Respond in a way that continues the conversation.
                                                                              The user is learning another language. 
                                                                              Keep responses simple.
                                                                              """
                        });
                    responseText = responseNpc.Content;
                    responseText = responseText.Replace("<think>", "").Replace("</think>", "");
                    ChatHistory.AddAssistantMessage(responseText);

                    status = "Translating Response...";

                    StateHasChanged();

                    var responseTranslationNPC = await TranslatorChatCompletionService
                        .GetChatMessageContentAsync(
                            $"""
                             Analyze the following english segment surrounded in triple backticks and generate a single refined {selectedLanguage.Substring(3)} translation. Only output the refined translation, do not explain.
                             The english segment:
                             ```{responseText}```
                             """);
                    npcTranslatedToLanguage = responseTranslationNPC.Content;

                    if (generateSpeech)
                    {

                        status = "Generating Speech...";

                        StateHasChanged();

                        var responseBytes = await ParlerApiClient.TextToSpeechAsync(npcTranslatedToLanguage, Configuration["npcVoice"] ?? "A female speaker delivers a slightly expressive and animated speech with a moderate speed and pitch. The recording is of very high quality, with the speaker's voice sounding clear and very close up.");
                        npcSpokenResponse = await context.DecodeAudioDataAsync(responseBytes);
                        await PlayResponse();
                    }

                    status = "Complete";

                    StateHasChanged();
                }
                catch (Exception e)
                {
                    error = e.ToString();
                }
            });
            await recorder.AddOnStopEventListenerAsync(stopEventListener);

            // Queue stop
            await recorder.StopAsync();
        }
        catch (WebIDLException ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private async Task PlayResponse()
    {
        if (context is null)
        {
            return;
        }

        await using AudioDestinationNode destination = await context.GetDestinationAsync();

        while (context is not null /*&& npcSpokenResponse is not null*/)
        {
            audioSourceNode = await AudioBufferSourceNode.CreateAsync(
                JSRuntime,
                context,
                new() { Buffer = npcSpokenResponse }
            );

            TaskCompletionSource ended = new();
            await using EventListener<Event> replayEnded = await EventListener<Event>.CreateAsync(JSRuntime, async _ =>
            {
                if (audioSourceNode is not null)
                {
                    await plot.Reset();
                }
                ended.SetResult();
            });
            await audioSourceNode.AddOnEndedEventListenerAsync(replayEnded);

            await audioSourceNode.ConnectAsync(destination);

            if (bufferAnalyzer is not null)
            {
                await bufferAnalyzer.DisposeAsync();
            }

            bufferAnalyzer = await AnalyserNode.CreateAsync(JSRuntime, context);
            await audioSourceNode.ConnectAsync(bufferAnalyzer);

            await audioSourceNode.StartAsync();

            StateHasChanged();

            await ended.Task;

            await audioSourceNode.RemoveOnEndedEventListenerAsync(replayEnded);
            await audioSourceNode.DisposeAsync();
            audioSourceNode = null;
        }
    }

    private async Task StopPlayingRecording()
    {
        if (mediaStream is not null)
        {
            await mediaStream.DisposeAsync();
            mediaStream = null;
        }
        if (recorder is not null)
        {
            await recorder.DisposeAsync();
            recorder = null;
        }
        if (audioBuffer is not null)
        {
            await audioBuffer.DisposeAsync();
            audioBuffer = null;
        }
        blobsRecorded.Clear();
        if (context is not null)
        {
            await context.DisposeAsync();
            context = null;
        }
        if (audioSourceNode is not null)
        {
            await audioSourceNode.StopAsync();
        }
        if (npcSpokenResponse != null)
        {
            await npcSpokenResponse.DisposeAsync();
            npcSpokenResponse = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopPlayingRecording();
    }
}