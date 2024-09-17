// ***********************************************************************
// Assembly         : EchoBot.Services
// Author           : JasonTheDeveloper
// Created          : 09-07-2020
//
// Last Modified By : bcage29
// Last Modified On : 10-17-2023
// ***********************************************************************
// <copyright file="BotMediaStream.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>
// <summary>The bot media stream.</summary>
// ***********************************************************************-
using AITeamAssistant.Media;
using AITeamAssistant.Service;
using AITeamAssistant.Util;
using AITeamAssistant.Action;
using API.Services.Interfaces;
using EchoBot.Media;
using EchoBot.Util;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Skype.Bots.Media;
using Microsoft.Skype.Internal.Media.Services.Common;
using System.Runtime.InteropServices;

namespace AITeamAssistant.Bot
{
    /// <summary>
    /// Class responsible for streaming audio and video.
    /// </summary>
    public class BotMediaStream : ObjectRootDisposable
    {
        private readonly AppSettings _settings;

        /// <summary>
        /// The participants
        /// </summary>
        internal List<IParticipant> participants;

        /// <summary>
        /// The audio socket
        /// </summary>
        private readonly IAudioSocket _audioSocket;
        /// <summary>
        /// The media stream
        /// </summary>
        private readonly ILogger _logger;
        private AudioVideoFramePlayer audioVideoFramePlayer;
        private readonly TaskCompletionSource<bool> audioSendStatusActive;
        private readonly TaskCompletionSource<bool> startVideoPlayerCompleted;
        private AudioVideoFramePlayerSettings audioVideoFramePlayerSettings;
        private List<AudioMediaBuffer> audioMediaBuffers = new List<AudioMediaBuffer>();
        private int shutdown;
        private readonly SpeechService _languageService;
        private readonly IOpenAIService _openAIService;
        private readonly IPromptFlowService promptFlowService;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="BotMediaStream" /> class.
        /// </summary>
        /// <param name="mediaSession">The media session.</param>
        /// <param name="callId">The call identity</param>
        /// <param name="graphLogger">The Graph logger.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="settings">Azure settings</param>
        /// <exception cref="InvalidOperationException">A mediaSession needs to have at least an audioSocket</exception>
        public BotMediaStream(
            ILocalMediaSession mediaSession,
            string callId,
            IGraphLogger graphLogger,
            ILogger logger,
            AppSettings settings,
            IOpenAIService openAIService,
            IPromptFlowService promptFlowService
            IOpenAIService openAIService,
            IConfiguration configuration
        )
            : base(graphLogger)
        {
            ArgumentVerifier.ThrowOnNullArgument(mediaSession, nameof(mediaSession));
            ArgumentVerifier.ThrowOnNullArgument(logger, nameof(logger));
            ArgumentVerifier.ThrowOnNullArgument(settings, nameof(settings));

            _configuration = configuration;
            _openAIService = openAIService;
            _settings = settings;
            _logger = logger;

            participants = new List<IParticipant>();

            audioSendStatusActive = new TaskCompletionSource<bool>();
            startVideoPlayerCompleted = new TaskCompletionSource<bool>();

            // Subscribe to the audio media.
            _audioSocket = mediaSession.AudioSocket;
            if (_audioSocket == null)
            {
                throw new InvalidOperationException("A mediaSession needs to have at least an audioSocket");
            }

            var ignoreTask = StartAudioVideoFramePlayerAsync().ForgetAndLogExceptionAsync(GraphLogger, "Failed to start the player");

            _audioSocket.AudioSendStatusChanged += OnAudioSendStatusChanged;

            _audioSocket.AudioMediaReceived += OnAudioMediaReceived;

            if (_settings.UseSpeechService)
            {
                _languageService = new SpeechService(_settings, _logger, _openAIService, promptFlowService);
                _languageService = new SpeechService(_settings, _logger, _openAIService, _configuration);
                _languageService.SendMediaBuffer += this.OnSendMediaBuffer;
            }
        }

        /// <summary>
        /// Gets the participants.
        /// </summary>
        /// <returns>List&lt;IParticipant&gt;.</returns>
        public List<IParticipant> GetParticipants()
        {
            return participants;
        }

        /// <summary>
        /// Shut down.
        /// </summary>
        /// <returns><see cref="Task" />.</returns>
        public async Task ShutdownAsync()
        {
            if (Interlocked.CompareExchange(ref shutdown, 1, 1) == 1)
            {
                return;
            }

            await startVideoPlayerCompleted.Task.ConfigureAwait(false);

            // unsubscribe
            if (_audioSocket != null)
            {
                _audioSocket.AudioSendStatusChanged -= OnAudioSendStatusChanged;
            }

            // shutting down the players
            if (audioVideoFramePlayer != null)
            {
                await audioVideoFramePlayer.ShutdownAsync().ConfigureAwait(false);
            }

            // make sure all the audio and video buffers are disposed, it can happen that,
            // the buffers were not enqueued but the call was disposed if the caller hangs up quickly
            foreach (var audioMediaBuffer in audioMediaBuffers)
            {
                audioMediaBuffer.Dispose();
            }

            _logger.LogInformation($"disposed {audioMediaBuffers.Count} audioMediaBUffers.");

            audioMediaBuffers.Clear();
        }

        /// <summary>
        /// Initialize AV frame player.
        /// </summary>
        /// <returns>Task denoting creation of the player with initial frames enqueued.</returns>
        private async Task StartAudioVideoFramePlayerAsync()
        {
            try
            {
                _logger.LogInformation("Send status active for audio and video Creating the audio video player");
                audioVideoFramePlayerSettings =
                    new AudioVideoFramePlayerSettings(new AudioSettings(20), new VideoSettings(), 1000);
                audioVideoFramePlayer = new AudioVideoFramePlayer(
                    (AudioSocket)_audioSocket,
                    null,
                    audioVideoFramePlayerSettings);

                _logger.LogInformation("created the audio video player");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create the audioVideoFramePlayer with exception");
            }
            finally
            {
                startVideoPlayerCompleted.TrySetResult(true);
            }
        }

        /// <summary>
        /// Callback for informational updates from the media plaform about audio status changes.
        /// Once the status becomes active, audio can be loopbacked.
        /// </summary>
        /// <param name="sender">The audio socket.</param>
        /// <param name="e">Event arguments.</param>
        private void OnAudioSendStatusChanged(object? sender, AudioSendStatusChangedEventArgs e)
        {
            _logger.LogTrace($"[AudioSendStatusChangedEventArgs(MediaSendStatus={e.MediaSendStatus})]");

            if (e.MediaSendStatus == MediaSendStatus.Active)
            {
                audioSendStatusActive.TrySetResult(true);
            }
        }

        /// <summary>
        /// Receive audio from subscribed participant.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The audio media received arguments.</param>
        private async void OnAudioMediaReceived(object? sender, AudioMediaReceivedEventArgs e)
        {
            _logger.LogTrace($"Received Audio: [AudioMediaReceivedEventArgs(Data=<{e.Buffer.Data.ToString()}>, Length={e.Buffer.Length}, Timestamp={e.Buffer.Timestamp})]");

            try
            {
                if (!startVideoPlayerCompleted.Task.IsCompleted) { return; }

                if (_languageService != null)
                {
                    // send audio buffer to language service for processing
                    // the particpant talking will hear the bot repeat what they said
                    await _languageService.AppendAudioBuffer(e.Buffer);
                    e.Buffer.Dispose();
                } else
                {
                    // send audio buffer back on the audio socket
                    // the particpant talking will hear themselves
                    var length = e.Buffer.Length;
                    if (length > 0)
                    {
                        var buffer = new byte[length];
                        Marshal.Copy(e.Buffer.Data, buffer, 0, (int)length);

                        var currentTick = DateTime.Now.Ticks;
                        audioMediaBuffers = Util.Utilities.CreateAudioMediaBuffers(buffer, currentTick, _logger);
                        await audioVideoFramePlayer.EnqueueBuffersAsync(audioMediaBuffers, new List<VideoMediaBuffer>());
                    }
                }
            }
            catch (Exception ex)
            {
                GraphLogger.Error(ex);
                _logger.LogError(ex, "OnAudioMediaReceived error");
            }
            finally
            {
                e.Buffer.Dispose();
            }
        }

        private void OnSendMediaBuffer(object? sender, Media.MediaStreamEventArgs e)
        {
            audioMediaBuffers = e.AudioMediaBuffers;
            var result = Task.Run(async () => await audioVideoFramePlayer.EnqueueBuffersAsync(audioMediaBuffers, new List<VideoMediaBuffer>())).GetAwaiter();
        }
    }
}

