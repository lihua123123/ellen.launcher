using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;

public interface IMetadataDictionaryIdBeyondItemSource
{
    ImmutableDictionary<BeyondItemId, BeyondItem> IdBeyondItemMap { get; set; }
}
