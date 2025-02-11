﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;

namespace VRCOSC.Game.ChatBox.Clips;

public class ClipState : IProvidesFormat
{
    public List<(string, string)> States { get; private init; } = null!;

    public Bindable<string> Format = new()
    {
        Default = string.Empty,
        Value = string.Empty
    };

    public Bindable<bool> Enabled = new();

    public List<string> ModuleNames => States.Select(state => state.Item1).ToList();
    public List<string> StateNames => States.Select(state => state.Item2).ToList();

    public bool IsDefault => Format.IsDefault && Enabled.IsDefault;

    public ClipState Copy(bool includeData = false)
    {
        var statesCopy = new List<(string, string)>();
        States.ForEach(state => statesCopy.Add(state));

        var copy = new ClipState
        {
            States = statesCopy
        };

        if (includeData)
        {
            copy.Format = Format.GetUnboundCopy();
            copy.Enabled = Enabled.GetUnboundCopy();
        }

        return copy;
    }

    private ClipState() { }

    public ClipState(ClipStateMetadata metadata)
    {
        States = new List<(string, string)> { new(metadata.Module, metadata.Lookup) };
        Format.Value = metadata.DefaultFormat;
        Format.Default = metadata.DefaultFormat;
    }

    public string GetFormat() => Format.Value;
}

public class ClipStateMetadata
{
    public required string Module { get; init; }
    public required string Lookup { get; init; }
    public required string Name { get; init; }
    public required string DefaultFormat { get; init; }
}
