// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Documents;
using Ellen.Launcher.UI.Xaml.Control.TextBlock.Syntax.MiHoYo;

namespace Ellen.Launcher.UI.Xaml.Control.TextBlock;

[DependencyProperty<Tuple<MiHoYoSyntaxLinkKind, uint>>("LinkData", IsAttached = true, TargetType = typeof(Hyperlink))]
public partial class DescriptionHyperLinkHelper
{
}