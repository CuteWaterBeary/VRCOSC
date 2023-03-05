﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;

namespace VRCOSC.Game.Graphics.ChatBox;

public partial class SelectedClipMetadataEditor : Container
{
    [BackgroundDependencyLoader]
    private void load()
    {
        Child = new Box
        {
            Colour = Color4.Black,
            RelativeSizeAxes = Axes.Both,
        };
    }
}
