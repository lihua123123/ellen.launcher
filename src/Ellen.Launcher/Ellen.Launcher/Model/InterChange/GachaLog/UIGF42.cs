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

    // 绝区零调频记录（UIGF v4.2 的 nap 字段）
    // ReSharper disable once InconsistentNaming
    [JsonPropertyName("nap")]
    [JsonPropertyOrder(3)]
    public ImmutableArray<UIGFEntry<NapItem>> Nap { get; set; } = [];
}
