// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.UI.Xaml.Control.TextBlock.Syntax.MiHoYo;

public sealed class MiHoYoSyntaxItalicElement : MiHoYoSyntaxElement
{
    public MiHoYoSyntaxItalicElement(TextPosition position, ImmutableArray<MiHoYoSyntaxElement> children)
        : base(position, children)
    {
    }
}