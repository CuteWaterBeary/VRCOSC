﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Logging;

namespace VRCOSC.Game.Modules;

public sealed class TerminalLogger
{
    private readonly string moduleName;

    public TerminalLogger(string moduleName)
    {
        this.moduleName = moduleName;
    }

    public void Log(string message)
    {
        message.Split('\n').ForEach(msg => Logger.Log($"[{moduleName}]: {msg}", "terminal"));
    }
}
