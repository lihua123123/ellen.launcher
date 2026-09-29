// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.UI.Xaml.Control;

[DependencyProperty<QualityType>("Quality", DefaultValue = QualityType.QUALITY_NONE, NotNull = true)]
[DependencyProperty<Uri>("Icon")]
[DependencyProperty<double>("IconOpacity", DefaultValue = 1.0, NotNull = true)]
[DependencyProperty<Uri>("Badge")]
public sealed partial class ItemIcon : Microsoft.UI.Xaml.Controls.Control;