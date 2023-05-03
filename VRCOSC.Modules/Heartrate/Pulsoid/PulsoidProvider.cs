﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using Newtonsoft.Json;
using VRCOSC.Game.Modules;
using VRCOSC.Modules.Heartrate.Pulsoid.Models;

namespace VRCOSC.Modules.Heartrate.Pulsoid;

public sealed class PulsoidProvider : HeartRateProvider
{
    private readonly string accessToken;

    protected override string WebSocketUrl => $"wss://dev.pulsoid.net/api/v1/data/real_time?access_token={accessToken}";

    public PulsoidProvider(string accessToken, TerminalLogger terminal)
        : base(terminal)
    {
        this.accessToken = accessToken;
    }

    protected override void HandleWsConnected()
    {
        Log(@"Successfully connected to the Pulsoid websocket");
    }

    protected override void HandleWsDisconnected()
    {
        Log(@"Disconnected from the Pulsoid websocket");
    }

    protected override void HandleWsMessage(string message)
    {
        var data = JsonConvert.DeserializeObject<PulsoidResponse>(message)!;
        OnHeartRateUpdate?.Invoke(data.Data.HeartRate);
    }
}
