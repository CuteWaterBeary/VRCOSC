﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SuperSocket.ClientEngine;
using VRCOSC.Game.Util;
using WebSocket4Net;

namespace VRCOSC.Game.Modules.Modules;

public abstract class BaseHeartRateProvider
{
    private readonly EventWaitHandle IsRunning = new AutoResetEvent(false);
    private readonly TerminalLogger terminal = new("HypeRateModule");

    private readonly WebSocket WebSocket;
    public Action? OnConnected;
    public Action? OnDisconnected;
    public Action<int>? OnHeartRateUpdate;

    protected BaseHeartRateProvider(string Uri)
    {
        terminal.Log("Creating base websocket");
        WebSocket = new WebSocket(Uri);
        WebSocket.Opened += wsConnected;
        WebSocket.Closed += wsDisconnected;
        WebSocket.MessageReceived += wsMessageReceived;
        WebSocket.Error += wsError;
    }

    public void Connect()
    {
        Task.Factory.StartNew(run, TaskCreationOptions.LongRunning);
    }

    public void Disconnect()
    {
        IsRunning.Set();
        WebSocket.Close();
    }

    protected void Send(object data)
    {
        WebSocket.Send(JsonConvert.SerializeObject(data));
    }

    private void run()
    {
        WebSocket.Open();
        IsRunning.WaitOne();
    }

    private void wsConnected(object? sender, EventArgs e)
    {
        terminal.Log("WebSocket successfully connected");
        OnConnected?.Invoke();
        OnWsConnected();
    }

    protected abstract void OnWsConnected();

    private void wsDisconnected(object? sender, EventArgs e)
    {
        terminal.Log("WebSocket disconnected");
        OnDisconnected?.Invoke();
        OnWsDisconnected();
        IsRunning.Set();
    }

    protected abstract void OnWsDisconnected();

    private void wsMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        terminal.Log(e.Message);
        OnWsMessageReceived(e.Message);
    }

    protected abstract void OnWsMessageReceived(string message);

    private void wsError(object? sender, ErrorEventArgs e)
    {
        terminal.Log(e.Exception.ToString());
    }
}
