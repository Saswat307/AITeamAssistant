// ***********************************************************************
// Assembly         : EchoBot.Services
// Author           : JasonTheDeveloper
// Created          : 09-07-2020
//
// Last Modified By : bcage29
// Last Modified On : 10-27-2023
// ***********************************************************************
// <copyright file="HeartbeatHandler.cs" company="Microsoft">
//     Copyright ©  2020
// </copyright>
// <summary></summary>
// ***********************************************************************>
using AITeamAssistant.Util;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using System.Timers;
using Timer = System.Timers.Timer;

namespace AITeamAssistant.Bot
{
    /// <summary>
    /// The base class for handling heartbeats.
    /// </summary>
    public abstract class HeartbeatHandler : ObjectRootDisposable
    {
        /// <summary>
        /// The heartbeat timer
        /// </summary>
        private readonly Timer heartbeatTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeartbeatHandler" /> class.
        /// </summary>
        /// <param name="frequency">The frequency of the heartbeat.</param>
        /// <param name="logger">The graph logger.</param>
        public HeartbeatHandler(TimeSpan frequency, IGraphLogger logger)
            : base(logger)
        {
            // initialize the timer
            var timer = new Timer(frequency.TotalMilliseconds);
            timer.Enabled = true;
            timer.AutoReset = true;
            timer.Elapsed += HeartbeatDetected;
            heartbeatTimer = timer;
        }

        /// <summary>
        /// This function is called whenever the heartbeat frequency has ellapsed.
        /// </summary>
        /// <param name="args">The elapsed event args.</param>
        /// <returns>The <see cref="Task" />.</returns>
        protected abstract Task HeartbeatAsync(ElapsedEventArgs args);

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            heartbeatTimer.Elapsed -= HeartbeatDetected;
            heartbeatTimer.Stop();
            heartbeatTimer.Dispose();
        }

        /// <summary>
        /// The heartbeat function.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The elapsed event args.</param>
        private void HeartbeatDetected(object sender, ElapsedEventArgs args)
        {
            var task = $"{GetType().FullName}.{nameof(this.HeartbeatAsync)}(args)";
            GraphLogger.Verbose($"Starting running task: " + task);
            _ = Task.Run(() => HeartbeatAsync(args)).ForgetAndLogExceptionAsync(GraphLogger, task);
        }
    }
}

