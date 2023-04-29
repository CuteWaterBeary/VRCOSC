﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.Game.Config;

namespace VRCOSC.Game.Graphics.Settings;

public sealed partial class UpdateSection : SectionContainer
{
    protected override string Title => "Update";

    protected override void GenerateItems()
    {
        AddDropdown("Update Mode", "How should VRCOSC handle updating?", ConfigManager.GetBindable<UpdateMode>(VRCOSCSetting.UpdateMode));
        AddToggle("Use PreRelease", "Update to the latest experimental version of VRCOSC", ConfigManager.GetBindable<bool>(VRCOSCSetting.UsePreRelease));
    }
}
