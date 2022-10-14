// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using VRCOSC.Game.Modules.Util;
using VRCOSC.Game.Util;
using VRCOSC.OSC;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

namespace VRCOSC.Game.Modules;

[SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly")]
public abstract class Module
{
    private GameHost Host = null!;
    private Storage Storage = null!;
    private OscClient OscClient = null!;
    private TerminalLogger Terminal = null!;
    private Bindable<ModuleState> State = null!;
    private TimedTask? updateTask;

    public readonly BindableBool Enabled = new();
    public readonly Dictionary<string, ModuleAttribute> Settings = new();
    public readonly Dictionary<string, ModuleAttribute> OutputParameters = new();
    private readonly Dictionary<Enum, InputParameterData> InputParameters = new();
    private readonly Dictionary<string, Enum> InputParametersMap = new();

    public virtual string Title => string.Empty;
    public virtual string Description => string.Empty;
    public virtual string Author => string.Empty;
    public virtual ModuleType ModuleType => ModuleType.General;
    public virtual string Prefab => string.Empty;
    protected virtual int DeltaUpdate => int.MaxValue;

    protected Player Player = null!;
    protected ChatBox ChatBox = null!;

    public void Initialise(GameHost host, Storage storage, OscClient oscClient, ChatBox chatBox)
    {
        Host = host;
        Storage = storage;
        OscClient = oscClient;
        ChatBox = chatBox;
        Terminal = new TerminalLogger(GetType().Name);
        State = new Bindable<ModuleState>(ModuleState.Stopped);
        Player = new Player(OscClient);

        CreateAttributes();
        performLoad();

        State.ValueChanged += _ => Log(State.Value.ToString());
    }

    #region Properties

    public bool HasSettings => Settings.Any();
    public bool HasOutgoingParameters => OutputParameters.Any();
    public bool HasAttributes => HasSettings || HasOutgoingParameters;

    private bool IsEnabled => Enabled.Value;
    private bool ShouldUpdate => DeltaUpdate != int.MaxValue;
    private string FileName => @$"{GetType().Name}.ini";

    private const string VRChatOscPrefix = @"/avatar/parameters/";

    #endregion

    #region Attributes

    protected virtual void CreateAttributes() { }

    protected void CreateSetting(Enum lookup, string displayName, string description, bool defaultValue)
        => addSingleSetting(lookup, displayName, description, defaultValue);

    protected void CreateSetting(Enum lookup, string displayName, string description, int defaultValue)
        => addSingleSetting(lookup, displayName, description, defaultValue);

    protected void CreateSetting(Enum lookup, string displayName, string description, string defaultValue)
        => addSingleSetting(lookup, displayName, description, defaultValue);

    protected void CreateSetting(Enum lookup, string displayName, string description, Enum defaultValue)
        => addSingleSetting(lookup, displayName, description, defaultValue);

    protected void CreateSetting(Enum lookup, string displayName, string description, int defaultValue, int minValue, int maxValue)
        => addRangedSetting(lookup, displayName, description, defaultValue, minValue, maxValue);

    protected void CreateSetting(Enum lookup, string displayName, string description, float defaultValue, float minValue, float maxValue)
        => addRangedSetting(lookup, displayName, description, defaultValue, minValue, maxValue);

    protected void CreateSetting(Enum lookup, string displayName, string description, IEnumerable<int> defaultValues)
        => addEnumerableSetting(lookup, displayName, description, defaultValues);

    protected void CreateSetting(Enum lookup, string displayName, string description, IEnumerable<string> defaultValues)
        => addEnumerableSetting(lookup, displayName, description, defaultValues);

    protected void CreateSetting(Enum lookup, string displayName, string description, string defaultValue, string buttonText, Action buttonAction)
        => addTextAndButtonSetting(lookup, displayName, description, defaultValue, buttonText, buttonAction);

    protected void CreateOutgoingParameter(Enum lookup, string displayName, string description, string defaultAddress)
        => addSingleOutgoingParameter(lookup, displayName, description, defaultAddress);

    protected void CreateOutgoingParameter(Enum lookup, string displayName, string description, IEnumerable<string> defaultAddresses)
        => addEnumerableOutgoingParameter(lookup, displayName, description, defaultAddresses);

    protected void RegisterIncomingParameter<T>(Enum lookup, string parameterNameOverride = "") where T : struct
        => registerInput(lookup, parameterNameOverride, new InputParameterData(typeof(T)));

    protected void RegisterButtonInput(Enum lookup, string parameterNameOverride = "")
        => registerInput(lookup, parameterNameOverride, new ButtonInputParameterData());

    protected void RegisterRadialInput(Enum lookup, string parameterNameOverride = "")
        => registerInput(lookup, parameterNameOverride, new RadialInputParameterData());

    private void registerInput(Enum lookup, string parameterNameOverride, InputParameterData parameterData)
    {
        var parameterName = string.IsNullOrEmpty(parameterNameOverride) ? lookup.ToString() : parameterNameOverride;
        InputParameters.Add(lookup, parameterData);
        InputParametersMap.Add(parameterName, lookup);
    }

    private void addSingleSetting(Enum lookup, string displayName, string description, object defaultValue)
    {
        Settings.Add(lookup.ToString().ToLowerInvariant(), new ModuleAttributeSingle(new ModuleAttributeMetadata(displayName, description), defaultValue));
    }

    private void addEnumerableSetting<T>(Enum lookup, string displayName, string description, IEnumerable<T> defaultValues)
    {
        Settings.Add(lookup.ToString().ToLowerInvariant(), new ModuleAttributeList(new ModuleAttributeMetadata(displayName, description), defaultValues.Cast<object>(), typeof(T)));
    }

    private void addRangedSetting<T>(Enum lookup, string displayName, string description, T defaultValue, T minValue, T maxValue) where T : struct
    {
        Settings.Add(lookup.ToString().ToLowerInvariant(), new ModuleAttributeSingleWithBounds(new ModuleAttributeMetadata(displayName, description), defaultValue, minValue, maxValue));
    }

    private void addTextAndButtonSetting(Enum lookup, string displayName, string description, string defaultValue, string buttonText, Action buttonAction)
    {
        Settings.Add(lookup.ToString().ToLowerInvariant(), new ModuleAttributeSingleWithButton(new ModuleAttributeMetadata(displayName, description), defaultValue, buttonText, buttonAction));
    }

    private void addSingleOutgoingParameter(Enum lookup, string displayName, string description, string defaultAddress)
    {
        OutputParameters.Add(lookup.ToString().ToLowerInvariant(), new ModuleAttributeSingle(new ModuleAttributeMetadata(displayName, description), defaultAddress));
    }

    private void addEnumerableOutgoingParameter(Enum lookup, string displayName, string description, IEnumerable<string> defaultAddresses)
    {
        OutputParameters.Add(lookup.ToString().ToLowerInvariant(), new ModuleAttributeList(new ModuleAttributeMetadata(displayName, description), defaultAddresses, typeof(string)));
    }

    #endregion

    #region Events

    internal void start()
    {
        if (!IsEnabled) return;

        State.Value = ModuleState.Starting;
        Player.Init();

        OnStart();

        if (ShouldUpdate) updateTask = new TimedTask(OnUpdate, DeltaUpdate, true).Start();

        OscClient.OnParameterReceived += onParameterReceived;

        State.Value = ModuleState.Started;
    }

    internal async Task stop()
    {
        if (!IsEnabled) return;

        State.Value = ModuleState.Stopping;

        OscClient.OnParameterReceived -= onParameterReceived;

        if (updateTask is not null) await updateTask.Stop();

        OnStop();

        Player.ResetAll();
        State.Value = ModuleState.Stopped;
    }

    protected virtual void OnStart() { }
    protected virtual void OnUpdate() { }
    protected virtual void OnStop() { }
    protected virtual void OnAvatarChange() { }
    protected virtual void OnPlayerStateUpdate(VRChatInputParameter key) { }

    #endregion

    #region Settings

    protected void SetSetting<T>(Enum lookup, T value)
    {
        var setting = (ModuleAttributeSingle)Settings[lookup.ToString().ToLowerInvariant()];
        setting.Attribute.Value = value!;
    }

    protected T GetSetting<T>(Enum lookup)
    {
        var setting = Settings[lookup.ToString().ToLowerInvariant()];

        object? value = setting switch
        {
            ModuleAttributeSingle settingSingle => settingSingle.Attribute.Value,
            ModuleAttributeList settingList => settingList.AttributeList.ToList(),
            _ => null
        };

        if (value is not T valueCast)
            throw new InvalidCastException($"Setting with lookup '{lookup}' is not of type '{nameof(T)}'");

        return valueCast;
    }

    #endregion

    #region IncomingParameters

    private void onParameterReceived(string address, object value)
    {
        if (address.StartsWith("/avatar/change"))
        {
            OnAvatarChange();
            return;
        }

        if (!address.StartsWith(VRChatOscPrefix)) return;

        var parameterName = address.Remove(0, VRChatOscPrefix.Length);
        updatePlayerState(parameterName, value);

        if (!InputParametersMap.TryGetValue(parameterName, out var key)) return;

        var inputParameterData = InputParameters[key];

        if (value.GetType() != inputParameterData.Type)
        {
            Log($@"Cannot accept input parameter. `{key}` expects type `{inputParameterData.Type}` but received type `{value.GetType()}`");
            return;
        }

        notifyParameterReceived(key, value, inputParameterData);
    }

    private void notifyParameterReceived(Enum key, object value, InputParameterData data)
    {
        switch (value)
        {
            case bool boolValue:
                OnBoolParameterReceived(key, boolValue);
                if (data.ActionMenu == ActionMenu.Button && boolValue) OnButtonPressed(key);
                break;

            case int intValue:
                OnIntParameterReceived(key, intValue);
                break;

            case float floatValue:
                OnFloatParameterReceived(key, floatValue);

                if (data.ActionMenu == ActionMenu.Radial)
                {
                    var radialData = (RadialInputParameterData)data;
                    OnRadialPuppetChange(key, new VRChatRadialPuppet(floatValue, radialData.PreviousValue));
                    radialData.PreviousValue = floatValue;
                }

                break;
        }
    }

    private void updatePlayerState(string parameterName, object value)
    {
        if (!Enum.TryParse(parameterName, out VRChatInputParameter vrChatInputParameter)) return;

        switch (vrChatInputParameter)
        {
            case VRChatInputParameter.Viseme:
                Player.Viseme = (Viseme)(int)value;
                break;

            case VRChatInputParameter.Voice:
                Player.Voice = (float)value;
                break;

            case VRChatInputParameter.GestureLeft:
                Player.GestureLeft = (Gesture)(int)value;
                break;

            case VRChatInputParameter.GestureRight:
                Player.GestureRight = (Gesture)(int)value;
                break;

            case VRChatInputParameter.GestureLeftWeight:
                Player.GestureLeftWeight = (float)value;
                break;

            case VRChatInputParameter.GestureRightWeight:
                Player.GestureRightWeight = (float)value;
                break;

            case VRChatInputParameter.AngularY:
                Player.AngularY = (float)value;
                break;

            case VRChatInputParameter.VelocityX:
                Player.VelocityX = (float)value;
                break;

            case VRChatInputParameter.VelocityY:
                Player.VelocityY = (float)value;
                break;

            case VRChatInputParameter.VelocityZ:
                Player.VelocityZ = (float)value;
                break;

            case VRChatInputParameter.Upright:
                Player.Upright = (float)value;
                break;

            case VRChatInputParameter.Grounded:
                Player.Grounded = (bool)value;
                break;

            case VRChatInputParameter.Seated:
                Player.Seated = (bool)value;
                break;

            case VRChatInputParameter.AFK:
                Player.AFK = (bool)value;
                break;

            case VRChatInputParameter.TrackingType:
                Player.TrackingType = (TrackingType)(int)value;
                break;

            case VRChatInputParameter.VRMode:
                Player.IsVR = (int)value == 1;
                break;

            case VRChatInputParameter.MuteSelf:
                Player.IsMuted = (bool)value;
                break;

            case VRChatInputParameter.InStation:
                Player.InStation = (bool)value;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(vrChatInputParameter), vrChatInputParameter, "Unknown VRChatInputParameter");
        }

        OnPlayerStateUpdate(vrChatInputParameter);
    }

    protected virtual void OnBoolParameterReceived(Enum key, bool value) { }
    protected virtual void OnIntParameterReceived(Enum key, int value) { }
    protected virtual void OnFloatParameterReceived(Enum key, float value) { }
    protected virtual void OnButtonPressed(Enum key) { }
    protected virtual void OnRadialPuppetChange(Enum key, VRChatRadialPuppet radialData) { }

    #endregion

    #region OutgoingParameters

    protected class OscAddress
    {
        private readonly OscClient oscClient;
        private readonly string address;

        public OscAddress(OscClient oscClient, string address)
        {
            this.oscClient = oscClient;
            this.address = address;
        }

        public void SendValue<T>(T value) where T : struct => oscClient.SendValue(address, value);
    }

    protected class OutputParameter : IEnumerable<OscAddress>
    {
        private readonly List<OscAddress> addresses = new();

        public OutputParameter(OscClient oscClient, IEnumerable<string> addressesStr)
        {
            addressesStr.ForEach(address => addresses.Add(new OscAddress(oscClient, address)));
        }

        public IEnumerator<OscAddress> GetEnumerator() => addresses.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    protected OutputParameter GetOutputParameter(Enum lookup)
    {
        var addresses = OutputParameters[lookup.ToString().ToLowerInvariant()];

        return addresses switch
        {
            ModuleAttributeSingle address => new OutputParameter(OscClient, new List<string>() { address.Attribute.Value.ToString()! }),
            ModuleAttributeList addressList => new OutputParameter(OscClient, addressList.GetValueList<string>()),
            _ => throw new InvalidCastException($"Unable to parse {nameof(ModuleAttribute)}")
        };
    }

    protected void SendParameter<T>(Enum lookup, T value) where T : struct
    {
        if (State.Value == ModuleState.Stopped) return;

        GetOutputParameter(lookup).ForEach(address => address.SendValue(value));
    }

    #endregion

    #region Loading

    private void performLoad()
    {
        using (var stream = Storage.GetStream(FileName))
        {
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);

                while (reader.ReadLine() is { } line)
                {
                    switch (line)
                    {
                        case "#InternalSettings":
                            performInternalSettingsLoad(reader);
                            break;

                        case "#Settings":
                            performSettingsLoad(reader);
                            break;

                        case "#OutputParameters":
                            performOutputParametersLoad(reader);
                            break;
                    }
                }
            }
        }

        executeAfterLoad();
    }

    private void performInternalSettingsLoad(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            if (line.Equals("#End")) break;

            var lineSplit = line.Split(new[] { '=' }, 2);
            var lookup = lineSplit[0];
            var value = lineSplit[1];

            switch (lookup)
            {
                case "enabled":
                    Enabled.Value = bool.Parse(value);
                    break;
            }
        }
    }

    private void performSettingsLoad(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            if (line.Equals("#End")) break;

            var lineSplitLookupValue = line.Split(new[] { '=' }, 2);
            var lookupType = lineSplitLookupValue[0].Split(new[] { ':' }, 2);
            var value = lineSplitLookupValue[1];

            var lookupStr = lookupType[0];
            var typeStr = lookupType[1];

            var lookup = lookupStr;
            if (lookupStr.Contains('#')) lookup = lookupStr.Split(new[] { '#' }, 2)[0];

            if (!Settings.ContainsKey(lookup)) continue;

            var setting = Settings[lookup];

            switch (setting)
            {
                case ModuleAttributeSingle settingSingle:
                {
                    var readableTypeName = settingSingle.Attribute.Value.GetType().ToReadableName().ToLowerInvariant();
                    if (!readableTypeName.Equals(typeStr)) continue;

                    switch (typeStr)
                    {
                        case "enum":
                            var typeAndValue = value.Split(new[] { '#' }, 2);
                            var enumType = enumNameToType(typeAndValue[0]);
                            if (enumType is not null) settingSingle.Attribute.Value = Enum.ToObject(enumType, int.Parse(typeAndValue[1]));
                            break;

                        case "string":
                            settingSingle.Attribute.Value = value;
                            break;

                        case "int":
                            settingSingle.Attribute.Value = int.Parse(value);
                            break;

                        case "float":
                            settingSingle.Attribute.Value = float.Parse(value);
                            break;

                        case "bool":
                            settingSingle.Attribute.Value = bool.Parse(value);
                            break;

                        default:
                            Logger.Log($"Unknown type found in file: {typeStr}");
                            break;
                    }

                    break;
                }

                case ModuleAttributeList settingList when settingList.AttributeList.Count == 0:
                    continue;

                case ModuleAttributeList settingList:
                {
                    var readableTypeName = settingList.AttributeList.First().Value.GetType().ToReadableName().ToLowerInvariant();
                    if (!readableTypeName.Equals(typeStr)) continue;

                    var index = int.Parse(lookupStr.Split(new[] { '#' }, 2)[1]);

                    switch (typeStr)
                    {
                        case "string":
                            settingList.AddAt(index, new Bindable<object>(value));
                            break;

                        case "int":
                            settingList.AddAt(index, new Bindable<object>(int.Parse(value)));
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown type found in file for {nameof(ModuleAttributeList)}: {value.GetType()}");
                    }

                    break;
                }
            }
        }
    }

    private void performOutputParametersLoad(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            if (line.Equals("#End")) break;

            var lineSplitLookupValue = line.Split(new[] { '=' }, 2);
            var lookupStr = lineSplitLookupValue[0];
            var value = lineSplitLookupValue[1];

            var lookup = lookupStr;
            if (lookupStr.Contains('#')) lookup = lookupStr.Split(new[] { '#' }, 2)[0];

            if (!OutputParameters.ContainsKey(lookup)) continue;

            var parameter = OutputParameters[lookup];

            switch (parameter)
            {
                case ModuleAttributeSingle parameterSingle:
                    parameterSingle.Attribute.Value = value;
                    break;

                case ModuleAttributeList parameterList:
                {
                    var index = int.Parse(lookupStr.Split(new[] { '#' }, 2)[1]);
                    parameterList.AddAt(index, new Bindable<object>(value));
                    break;
                }
            }
        }
    }

    private void executeAfterLoad()
    {
        performSave();

        Enabled.BindValueChanged(_ => performSave());
        Settings.Values.ForEach(handleAttributeBind);
        OutputParameters.Values.ForEach(handleAttributeBind);
    }

    private void handleAttributeBind(ModuleAttribute value)
    {
        switch (value)
        {
            case ModuleAttributeSingle valueSingle:
                valueSingle.Attribute.BindValueChanged(_ => performSave());
                break;

            case ModuleAttributeList valueList:
                valueList.AttributeList.BindCollectionChanged((_, e) =>
                {
                    if (e.NewItems is not null)
                    {
                        foreach (var newItem in e.NewItems)
                        {
                            var bindable = (Bindable<object>)newItem;
                            bindable.BindValueChanged(_ => performSave());
                        }
                    }

                    performSave();
                });
                break;
        }
    }

    private static Type? enumNameToType(string enumName) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(enumName)).FirstOrDefault(type => type?.IsEnum ?? false);

    #endregion

    #region Saving

    private void performSave()
    {
        using var stream = Storage.CreateFileSafely(FileName);
        using var writer = new StreamWriter(stream);

        performInternalSettingsSave(writer);
        performSettingsSave(writer);
        performOutputParametersSave(writer);
    }

    private void performInternalSettingsSave(TextWriter writer)
    {
        writer.WriteLine(@"#InternalSettings");
        writer.WriteLine(@"{0}={1}", "enabled", Enabled.Value.ToString());
        writer.WriteLine(@"#End");
    }

    private void performSettingsSave(TextWriter writer)
    {
        var areAllDefault = Settings.All(pair => pair.Value.IsDefault());
        if (areAllDefault) return;

        writer.WriteLine(@"#Settings");

        foreach (var (lookup, moduleAttributeData) in Settings)
        {
            if (moduleAttributeData.IsDefault()) continue;

            switch (moduleAttributeData)
            {
                case ModuleAttributeSingle moduleAttributeSingle:
                {
                    var value = moduleAttributeSingle.Attribute.Value;
                    var valueType = value.GetType();
                    var readableTypeName = valueType.ToReadableName().ToLowerInvariant();

                    if (valueType.IsSubclassOf(typeof(Enum)))
                    {
                        var enumClass = valueType.FullName;
                        writer.WriteLine(@"{0}:{1}={2}#{3}", lookup, readableTypeName, enumClass, (int)value);
                    }
                    else
                    {
                        writer.WriteLine(@"{0}:{1}={2}", lookup, readableTypeName, value);
                    }

                    break;
                }

                case ModuleAttributeList moduleAttributeList when moduleAttributeList.AttributeList.Count == 0:
                    continue;

                case ModuleAttributeList moduleAttributeList:
                {
                    var values = moduleAttributeList.AttributeList.ToList();
                    var valueType = values.First().Value.GetType();
                    var readableTypeName = valueType.ToReadableName().ToLowerInvariant();

                    for (int i = 0; i < values.Count; i++)
                    {
                        writer.WriteLine(@"{0}#{1}:{2}={3}", lookup, i, readableTypeName, values[i].Value);
                    }

                    break;
                }
            }
        }

        writer.WriteLine(@"#End");
    }

    private void performOutputParametersSave(TextWriter writer)
    {
        var areAllDefault = OutputParameters.All(pair => pair.Value.IsDefault());
        if (areAllDefault) return;

        writer.WriteLine(@"#OutputParameters");

        foreach (var (lookup, moduleAttributeData) in OutputParameters)
        {
            if (moduleAttributeData.IsDefault()) continue;

            switch (moduleAttributeData)
            {
                case ModuleAttributeSingle moduleAttributeSingle:
                {
                    var value = moduleAttributeSingle.Attribute.Value;
                    writer.WriteLine(@"{0}={1}", lookup, value);
                    break;
                }

                case ModuleAttributeList moduleAttributeList:
                {
                    var values = moduleAttributeList.AttributeList.ToList();

                    for (int i = 0; i < values.Count; i++)
                    {
                        writer.WriteLine(@"{0}#{1}={2}", lookup, i, values[i].Value);
                    }

                    break;
                }
            }
        }

        writer.WriteLine(@"#End");
    }

    #endregion

    #region Extensions

    protected void Log(string message) => Terminal.Log(message);

    protected void OpenUrlExternally(string Url) => Host.OpenUrlExternally(Url);

    #endregion

    public enum ModuleState
    {
        Starting,
        Started,
        Stopping,
        Stopped
    }
}
