﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.Game;
using VRCOSC.Game.Modules;

namespace VRCOSC.Modules.Random;

public abstract class RandomModule<T> : Module where T : struct
{
    public override string Title => $"Random {typeof(T).ToReadableName()}";
    public override string Description => $"Sends a random {typeof(T).ToReadableName().ToLowerInvariant()} over a variable time period";
    public override string Author => "VolcanicArts";
    public override ModuleType Type => ModuleType.General;
    protected override TimeSpan DeltaUpdate => TimeSpan.FromMilliseconds(GetSetting<int>(RandomSetting.DeltaUpdate));

    private readonly System.Random random = new();

    protected override void CreateAttributes()
    {
        CreateSetting(RandomSetting.DeltaUpdate, "Time Between Value", "The amount of time, in milliseconds, between each random value", 1000);

        CreateParameter<T>(RandomParameter.RandomValue, ParameterMode.Write, $"VRCOSC/Random{typeof(T).ToReadableName()}", $"Random {typeof(T).ToReadableName()}", $"A random {typeof(T).ToReadableName()}");
    }

    protected override void OnModuleUpdate()
    {
        SendParameter(RandomParameter.RandomValue, GetRandomValue());
    }

    protected abstract T GetRandomValue();

    protected float RandomFloat(float min = float.MinValue, float max = float.MaxValue) => Map(random.NextSingle(), 0f, 1f, min, max);

    protected int RandomInt(int min = int.MinValue, int max = int.MaxValue) => (int)RandomFloat(min, max);

    protected bool RandomBool() => (int)MathF.Round(RandomFloat(0, 1)) == 1;

    private enum RandomSetting
    {
        DeltaUpdate
    }

    private enum RandomParameter
    {
        RandomValue
    }
}
