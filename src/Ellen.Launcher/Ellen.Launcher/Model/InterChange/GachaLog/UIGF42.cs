using System.Collections.Immutable;

namespace Ellen.Launcher.Model.InterChange.GachaLog;

// This class unfortunately can't use required properties because it's been rooted in XamlTypeInfo
// ReSharper disable once InconsistentNaming
public class UIGF42 : UIGF4
{
    // ReSharper disable once InconsistentNaming
    [JsonPropertyName("hk4e_ugc")]
    [JsonPropertyOrder(2)]
    public ImmutableArray<UIGFEntry<Hk4eUGCItem>> Hk4eUgc { get; set; } = [];
}
