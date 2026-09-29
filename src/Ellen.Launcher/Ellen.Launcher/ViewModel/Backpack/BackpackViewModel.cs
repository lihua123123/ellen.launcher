// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Service.Backpack;
using Ellen.Launcher.Service.Game.Island;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.Yae.PlayerStore;
using Ellen.Launcher.UI.Xaml.Control.AutoSortBox;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using Ellen.Launcher.ViewModel.Game;

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ellen.Launcher.ViewModel.Backpack;

[Service(ServiceLifetime.Scoped)]
public sealed partial class BackpackViewModel : Abstraction.ViewModel
{
    private readonly BackpackViewModelScopeContext scopeContext;
    private readonly IEmbeddedYaeGate embeddedYaeGate;
    private readonly ExclusiveTokenProvider itemsTokenProvider = new();
    private ImmutableDictionary<BackpackItemCategory, ImmutableArray<BackpackItemView>> categoryItems = [];
    private ImmutableDictionary<BackpackItemCategory, ImmutableArray<BackpackItemView>> publishedItems = [];
    private FrozenDictionary<uint, int> foodQualityMap = FrozenDictionary<uint, int>.Empty;
    private FrozenDictionary<uint, CookFoodType> foodTypeMap = FrozenDictionary<uint, CookFoodType>.Empty;
    private ImmutableDictionary<BackpackItemCategory, FrozenDictionary<string, SearchToken>> categoryTokens = [];
    private ImmutableDictionary<BackpackItemCategory, ImmutableArray<AutoSortToken>> categorySortTokens = [];

    private static readonly ImmutableArray<BackpackItemCategory> CategoryIndexMap = [
        BackpackItemCategory.Weapon,
        BackpackItemCategory.Reliquary,
        BackpackItemCategory.UpgradeItem,
        BackpackItemCategory.Food,
        BackpackItemCategory.Material,
        BackpackItemCategory.Gadget,
        BackpackItemCategory.Quest,
        BackpackItemCategory.PreciousItem,
        BackpackItemCategory.Unknown,
    ];

    [GeneratedConstructor]
    public partial BackpackViewModel(IServiceProvider serviceProvider);

    public IAdvancedDbCollectionView<BackpackArchive>? Archives
    {
        get;
        set
        {
            AdvancedCollectionViewCurrentChanged.Detach(field, OnCurrentArchiveChanged);
            SetProperty(ref field, value);
            AdvancedCollectionViewCurrentChanged.Attach(value, OnCurrentArchiveChanged);
        }
    }

    // One list per category instead of a single shared "current" list: every PivotItem keeps its own
    // GridView alive while it is realized, so binding all of them to one property made a category
    // switch (and every filter/sort change) rebuild the containers of all realized tabs at once.
    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> WeaponItems { get; set; } = [];

    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> ReliquaryItems { get; set; } = [];

    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> UpgradeItemItems { get; set; } = [];

    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> FoodItems { get; set; } = [];

    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> MaterialItems { get; set; } = [];

    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> GadgetItems { get; set; } = [];

    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> QuestItems { get; set; } = [];

    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> PreciousItemItems { get; set; } = [];

    /// <summary>
    /// 「未分类」标签页。物品的 <see cref="BackpackItemCategory"/> 为 <c>Unknown</c>
    /// （即 <c>IdMaterialMap</c> 缺这个 ItemId 的元数据）时进入这里。
    /// 不再静默并入 Material 桶（那会把未必是材料、只是缺元数据的物品误标为材料）。
    /// </summary>
    [ObservableProperty]
    public partial ImmutableArray<BackpackItemView> UnknownItems { get; set; } = [];

    [ObservableProperty]
    public partial int SelectedCategoryIndex { get; set; }

    [ObservableProperty]
    public partial SearchData? SearchData { get; set; }

    [ObservableProperty]
    public partial ImmutableArray<AutoSortToken> AvailableSortTokens { get; set; } = [];

    [Command("ExportCsvCommand")]
    private async Task ExportCsvAsync()
    {
        if (Archives?.CurrentItem is not { } archive)
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewPageBackpackExportNoArchive));
            return;
        }

        FileSystemPickerOptions options = new()
        {
            Title = SH.ViewPageBackpackExportCsv,
            DefaultFileName = $"backpack_{archive.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            FilterName = "CSV",
            FilterType = "*.csv",
        };

        if (scopeContext.FileSystemPickerInteraction.SaveFile(options) is not (true, { HasValue: true } file))
        {
            return;
        }

        try
        {
            StringBuilder builder = new();
            builder.AppendLine("分类,品质,名称,类型,说明,数量,等级,精炼,套装,主属性,主属性值,副词条");

            int count = 0;
            foreach ((BackpackItemCategory category, ImmutableArray<BackpackItemView> items) in categoryItems)
            {
                foreach (BackpackItemView item in items)
                {
                    builder.AppendLine(string.Join(',', [
                        Csv(category.ToString()),
                        Csv(((int)item.Quality).ToString(CultureInfo.InvariantCulture)),
                        Csv(item.Name),
                        Csv(item.TypeDescription),
                        Csv(item.Description),
                        Csv(item.Entity.Count.ToString(CultureInfo.InvariantCulture)),
                        Csv(item.Entity.Level.ToString(CultureInfo.InvariantCulture)),
                        Csv(item is BackpackWeaponItemView weapon ? weapon.RefinementRank.ToString(CultureInfo.InvariantCulture) : string.Empty),
                        Csv(item is BackpackReliquaryItemView reliquary ? reliquary.SetName : string.Empty),
                        Csv(item is BackpackReliquaryItemView mainProp ? mainProp.MainPropName : string.Empty),
                        Csv(item is BackpackReliquaryItemView mainPropValue ? mainPropValue.MainPropValue : string.Empty),
                        Csv(item is BackpackReliquaryItemView subStats ? string.Join(' ', subStats.SubStats.Select(static subStat => $"{subStat.FightProp}:{subStat.Value.ToString(CultureInfo.InvariantCulture)}")) : string.Empty),
                    ]));
                    count++;
                }
            }

            await File.WriteAllTextAsync(file, builder.ToString(), new UTF8Encoding(true)).ConfigureAwait(false);
            scopeContext.Messenger.Send(InfoBarMessage.Success(SH.ViewPageBackpackExportSuccessTitle, SH.FormatViewPageBackpackExportSuccessMessage(count, file)));
        }
        catch (Exception ex)
        {
            scopeContext.Messenger.Send(InfoBarMessage.Error(SH.ViewPageBackpackExportErrorTitle, ex));
        }
    }

    [Command("ExportJsonCommand")]
    private async Task ExportJsonAsync()
    {
        if (Archives?.CurrentItem is not { } archive)
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewPageBackpackExportNoArchive));
            return;
        }

        FileSystemPickerOptions options = new()
        {
            Title = SH.ViewPageBackpackExportJson,
            DefaultFileName = $"backpack_{archive.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            FilterName = "JSON",
            FilterType = "*.json",
        };

        if (scopeContext.FileSystemPickerInteraction.SaveFile(options) is not (true, { HasValue: true } file))
        {
            return;
        }

        try
        {
            int count = categoryItems.Sum(static pair => pair.Value.Length);
            await File.WriteAllBytesAsync(file, BuildJsonBytes(archive.Name)).ConfigureAwait(false);
            scopeContext.Messenger.Send(InfoBarMessage.Success(SH.ViewPageBackpackExportSuccessTitle, SH.FormatViewPageBackpackExportSuccessMessage(count, file)));
        }
        catch (Exception ex)
        {
            scopeContext.Messenger.Send(InfoBarMessage.Error(SH.ViewPageBackpackExportErrorTitle, ex));
        }
    }

    private byte[] BuildJsonBytes(string archiveName)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("archive", archiveName);
            writer.WriteString("exportedAt", DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteStartArray("items");

            foreach ((BackpackItemCategory category, ImmutableArray<BackpackItemView> items) in categoryItems)
            {
                foreach (BackpackItemView item in items)
                {
                    writer.WriteStartObject();
                    writer.WriteString("category", category.ToString());
                    writer.WriteNumber("itemId", item.Entity.ItemId);
                    writer.WriteString("name", item.Name);
                    writer.WriteNumber("quality", (int)item.Quality);
                    writer.WriteNumber("count", item.Entity.Count);
                    writer.WriteNumber("level", item.Entity.Level);
                    writer.WriteString("type", item.TypeDescription);
                    writer.WriteString("description", item.Description);

                    if (item is BackpackWeaponItemView weapon)
                    {
                        writer.WriteNumber("refinement", weapon.RefinementRank);
                        writer.WriteString("weaponType", weapon.WeaponTypeName);
                    }

                    if (item is BackpackReliquaryItemView reliquary)
                    {
                        writer.WriteString("setName", reliquary.SetName);
                        writer.WriteString("equipType", reliquary.EquipTypeName);
                        writer.WriteString("mainProp", reliquary.MainPropName);
                        writer.WriteString("mainPropValue", reliquary.MainPropValue);
                        writer.WriteStartArray("subStats");
                        foreach (BackpackReliquarySubStatView subStat in reliquary.SubStats)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("prop", subStat.FightProp.ToString());
                            writer.WriteNumber("value", subStat.Value);
                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                    }

                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;

        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        // Set empty SearchData so AutoSuggestTokenBox has a non-null binding target
        SearchData = SearchData.Create(FrozenDictionary<string, SearchToken>.Empty);

        if (!await scopeContext.MetadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        token.ThrowIfCancellationRequested();

        IAdvancedDbCollectionView<BackpackArchive> archives;
        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            archives = await scopeContext.BackpackService.GetArchiveCollectionAsync().ConfigureAwait(false);
        }

        await scopeContext.TaskContext.SwitchToMainThreadAsync();

        Archives = archives;
        Archives.MoveCurrentTo(Archives.Source.SelectedOrFirstOrDefault());

        UpdateItemsAsync(Archives.CurrentItem, itemsTokenProvider.GetNewToken()).SafeForget();

        return true;
    }

    protected override void UninitializeOverride()
    {
        using (Archives?.SuppressChangeCurrentItem())
        {
            Archives = default;
        }

        ClearCategoryItems();
    }

    private void OnCurrentArchiveChanged(object? sender, object? e)
    {
        UpdateItemsAsync(Archives?.CurrentItem, itemsTokenProvider.GetNewToken()).SafeForget();
    }

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        BackpackItemCategory category = GetSelectedCategory();
        BuildSearchData(category);
        BuildSortTokens(category);
        UpdateItemsFilter(category);
    }

    [Command("ApplySortCommand")]
    private void ApplySort()
    {
        UpdateItemsFilter(GetSelectedCategory());
    }

    [Command("AddArchiveCommand")]
    private async Task AddArchiveAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Add archive", "BackpackViewModel.Command"));

        if (Archives is null)
        {
            return;
        }

        BackpackArchiveCreateDialog dialog = await scopeContext.ContentDialogFactory.CreateInstanceAsync<BackpackArchiveCreateDialog>(scopeContext.ServiceProvider).ConfigureAwait(false);
        if (await dialog.GetInputAsync().ConfigureAwait(false) is not (true, { } name))
        {
            return;
        }

        BackpackArchive added = scopeContext.BackpackService.AddArchive(name);

        IAdvancedDbCollectionView<BackpackArchive> archives = await scopeContext.BackpackService.GetArchiveCollectionAsync().ConfigureAwait(false);
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        Archives = archives;

        BackpackArchive? current = Archives.Source.FirstOrDefault(a => a.InnerId == added.InnerId);
        Archives.MoveCurrentTo(current ?? Archives.Source.FirstOrDefault());

        scopeContext.Messenger.Send(InfoBarMessage.Success(SH.FormatViewPageBackpackArchiveAdded(name)));
    }

    [Command("RemoveArchiveCommand")]
    private async Task RemoveArchiveAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Remove archive", "BackpackViewModel.Command"));

        if (Archives?.CurrentItem is not { } current)
        {
            return;
        }

        ContentDialogResult result = await scopeContext.ContentDialogFactory
            .CreateForConfirmCancelAsync(
                SH.FormatViewPageBackpackRemoveArchiveTitle(current.Name),
                SH.ViewPageBackpackRemoveArchiveContent)
            .ConfigureAwait(false);

        if (result is not ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                scopeContext.BackpackService.RemoveArchive(current);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected when the user rapidly switches archives — the CTS cancels the in-flight save.
        }

        IAdvancedDbCollectionView<BackpackArchive> archives = await scopeContext.BackpackService.GetArchiveCollectionAsync().ConfigureAwait(false);
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        Archives = archives;
        Archives.MoveCurrentTo(Archives.Source.SelectedOrFirstOrDefault());
    }

    [Command("RefreshByEmbeddedYaeCommand")]
    private async Task RefreshByEmbeddedYaeAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Refresh backpack", "BackpackViewModel.Command", [("source", "Embedded Yae")]));

        if (!EllenRuntime.IsProcessElevated)
        {
            await scopeContext.ContentDialogFactory
                .CreateForConfirmAsync(SH.ViewModelYaeProcessNotElevatedTitle, SH.ViewModelYaeProcessNotElevatedDescription)
                .ConfigureAwait(false);
            return;
        }

        if (!await embeddedYaeGate.EnsureConfirmedAsync().ConfigureAwait(false))
        {
            return;
        }

        if (Archives?.CurrentItem is not { } archive)
        {
            return;
        }

        EmbeddedYaeLaunchExecutionViewModel viewModel = scopeContext.ServiceProvider.GetRequiredService<EmbeddedYaeLaunchExecutionViewModel>();
        if (!await viewModel.InitializeAsync().ConfigureAwait(false))
        {
            return;
        }

        PlayerStoreResult? storeResult = await scopeContext.YaeService.GetPlayerStoreResultAsync(viewModel).ConfigureAwait(false);

        if (storeResult is null)
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewPageBackpackRefreshWarning));
            return;
        }

        if (scopeContext.BackpackService.RefreshByEmbeddedYae(archive, storeResult))
        {
            scopeContext.Messenger.Send(InfoBarMessage.Success(SH.ViewPageBackpackRefreshSuccess));
        }
        else
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewPageBackpackRefreshWarning));
        }

        await UpdateItemsAsync(archive, itemsTokenProvider.GetNewToken()).ConfigureAwait(false);
    }

    private async ValueTask UpdateItemsAsync(BackpackArchive? archive, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (archive is null)
        {
            await scopeContext.TaskContext.InvokeOnMainThreadAsync(ClearCategoryItems).ConfigureAwait(false);
            categoryItems = [];
            return;
        }

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, CancellationToken);
        BackpackServiceMetadataContext context = await scopeContext.MetadataService
            .GetContextAsync<BackpackServiceMetadataContext>(linkedCts.Token)
            .ConfigureAwait(false);

        BackpackItemCategory selected = GetSelectedCategory();

        // Build everything on the thread pool. The metadata await above usually completes synchronously
        // (the context is cached), and ConfigureAwait(false) on an already-completed task does NOT hop
        // threads — so without Task.Run this whole projection ran on the UI thread and froze the window
        // for as long as it took to build, sort and tokenize thousands of item views.
        await Task.Run(
            () =>
            {
                ImmutableArray<BackpackItemView> allItems = [.. scopeContext.BackpackService
                    .GetBackpackItemImmutableArrayByArchiveId(archive.InnerId)
                    .Select(item => BackpackItemView.Create(item, context))];

                categoryItems = BuildCategoryViews(allItems);

                // Build food quality/type reverse lookup maps
                Dictionary<uint, int> qualityMap = [];
                Dictionary<uint, CookFoodType> typeMap = [];
                foreach (ref readonly CookRecipe recipe in context.CookRecipes.AsSpan())
                {
                    ImmutableArray<IdCount> outputs = recipe.QualityOutput;
                    for (int i = 0; i < outputs.Length; i++)
                    {
                        qualityMap.TryAdd(outputs[i].Id, i);
                        typeMap.TryAdd(outputs[i].Id, recipe.FoodType);
                    }
                }

                foodQualityMap = qualityMap.ToFrozenDictionary();
                foodTypeMap = typeMap.ToFrozenDictionary();

                // Pre-build token dictionaries for all categories
                ImmutableDictionary<BackpackItemCategory, FrozenDictionary<string, SearchToken>>.Builder tokenBuilder =
                    ImmutableDictionary.CreateBuilder<BackpackItemCategory, FrozenDictionary<string, SearchToken>>();
                foreach (BackpackItemCategory cat in Enum.GetValues<BackpackItemCategory>())
                {
                    ImmutableArray<BackpackItemView> catItems = categoryItems.GetValueOrDefault(cat, []);
                    tokenBuilder.Add(cat, BackpackFilterTokenBuilder.Build(cat, catItems));
                }

                categoryTokens = tokenBuilder.ToImmutable();
                categorySortTokens = BackpackSortTokenBuilder.Build();
            },
            linkedCts.Token).ConfigureAwait(false);

        // Swap to new items atomically on main thread — avoids flash of empty list
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();

        // Publish only the visible category. Pivot measures every PivotItem it holds, so handing a list to
        // all nine categories at once makes all nine GridViews realize a screenful of containers (and kick
        // off their image loads) simultaneously — that is what made opening this page, switching archives
        // and refreshing so slow. Each remaining category is published the first time its tab is selected
        // (see OnSelectedCategoryIndexChanged); until then it stays empty and realizes nothing.
        publishedItems = [];
        foreach (BackpackItemCategory other in CategoryIndexMap)
        {
            if (other != selected)
            {
                SetCategoryItems(other, []);
            }
        }

        BuildSearchData(selected);
        BuildSortTokens(selected);
        UpdateItemsFilter(selected);
    }

    private void SetCategoryItems(BackpackItemCategory category, ImmutableArray<BackpackItemView> items)
    {
        switch (category)
        {
            case BackpackItemCategory.Weapon:
                WeaponItems = items;
                break;
            case BackpackItemCategory.Reliquary:
                ReliquaryItems = items;
                break;
            case BackpackItemCategory.UpgradeItem:
                UpgradeItemItems = items;
                break;
            case BackpackItemCategory.Food:
                FoodItems = items;
                break;
            case BackpackItemCategory.Material:
                MaterialItems = items;
                break;
            case BackpackItemCategory.Gadget:
                GadgetItems = items;
                break;
            case BackpackItemCategory.Quest:
                QuestItems = items;
                break;
            case BackpackItemCategory.PreciousItem:
                PreciousItemItems = items;
                break;
            case BackpackItemCategory.Unknown:
                UnknownItems = items;
                break;
        }
    }

    private void ClearCategoryItems()
    {
        publishedItems = [];
        WeaponItems = [];
        ReliquaryItems = [];
        UpgradeItemItems = [];
        FoodItems = [];
        MaterialItems = [];
        GadgetItems = [];
        QuestItems = [];
        PreciousItemItems = [];
        UnknownItems = [];
    }

    [Command("FilterCommand")]
    private void ApplyFilter()
    {
        UpdateItemsFilter(GetSelectedCategory());
    }

    private BackpackItemCategory GetSelectedCategory()
    {
        int index = SelectedCategoryIndex;
        return index >= 0 && index < CategoryIndexMap.Length
            ? CategoryIndexMap[index]
            : BackpackItemCategory.Weapon;
    }

    private void BuildSearchData(BackpackItemCategory category)
    {
        SearchData = SearchData.Create(categoryTokens.GetValueOrDefault(category, FrozenDictionary<string, SearchToken>.Empty));
    }

    private void BuildSortTokens(BackpackItemCategory category)
    {
        if (categorySortTokens.TryGetValue(category, out ImmutableArray<AutoSortToken> tokens))
        {
            AvailableSortTokens = tokens;
        }
    }

    private void UpdateItemsFilter(BackpackItemCategory category)
    {
        ImmutableArray<BackpackItemView> items = categoryItems.GetValueOrDefault(category, []);
        Predicate<BackpackItemView>? predicate = BackpackFilter.Compile(SearchData, foodQualityMap, foodTypeMap);
        ImmutableArray<BackpackItemView> filtered = predicate is null ? items : [.. items.Where(item => predicate(item))];

        // Items in categoryItems are already sorted with default sort; only re-sort when custom sort is active
        ImmutableArray<BackpackItemView> result = new AutoSortData<BackpackItemView>(AvailableSortTokens, BackpackSortComparer.CompareByKind).Compile() is { } comparer
            ? [.. filtered.OrderBy(x => x, comparer)]
            : filtered;

        // Assigning ItemsSource makes the GridView tear down and recreate every realized container, so
        // skip the assignment when the outcome is identical to what is already published — the common
        // case when flipping between tabs that share the same (unchanged) filter and sort state.
        if (publishedItems.TryGetValue(category, out ImmutableArray<BackpackItemView> published)
            && published.Length == result.Length
            && published.AsSpan().SequenceEqual(result.AsSpan()))
        {
            return;
        }

        publishedItems = publishedItems.SetItem(category, result);
        SetCategoryItems(category, result);
    }

    private static ImmutableArray<BackpackItemView> ApplyDefaultSort(ImmutableArray<BackpackItemView> items, BackpackItemCategory category)
    {
        return category switch
        {
            BackpackItemCategory.Weapon => [.. items
                .Cast<BackpackWeaponItemView>()
                .OrderByDescending(w => w.Weapon.RankLevel)
                .ThenByDescending(w => w.Level)
                .ThenBy(w => w.Entity.ItemId)],
            BackpackItemCategory.Reliquary => [.. items
                .Cast<BackpackReliquaryItemView>()
                .OrderByDescending(r => r.Level)
                .ThenBy(r => r.Reliquary.SortOrder)],
            _ => [.. items
                .OrderByDescending(BackpackSortComparer.GetQualityRank)
                .ThenBy(item => item.Entity.ItemId)],
        };
    }

    private static ImmutableDictionary<BackpackItemCategory, ImmutableArray<BackpackItemView>> BuildCategoryViews(ImmutableArray<BackpackItemView> all)
    {
        ImmutableDictionary<BackpackItemCategory, ImmutableArray<BackpackItemView>>.Builder builder =
            ImmutableDictionary.CreateBuilder<BackpackItemCategory, ImmutableArray<BackpackItemView>>();

        foreach (BackpackItemCategory cat in Enum.GetValues<BackpackItemCategory>())
        {
            ImmutableArray<BackpackItemView> items = [.. all
                .Where(item => item.Category == cat && IsCorrectType(item, cat))];

            builder.Add(cat, ApplyDefaultSort(items, cat));
        }

        // 每个分类（含 Unknown）都有自己的桶，Unknown 由「未分类」标签页显示。
        // 不再把 Unknown 静默并入 Material —— 那会把「元数据缺失、未必是材料」的物品误标成材料，
        // 并让 Material 桶与 CSV/JSON 导出重复计入。根治办法是补齐 IdMaterialMap。
        return builder.ToImmutable();
    }

    private static bool IsCorrectType(BackpackItemView item, BackpackItemCategory category)
    {
        return category switch
        {
            BackpackItemCategory.Weapon => item is BackpackWeaponItemView,
            BackpackItemCategory.Reliquary => item is BackpackReliquaryItemView,
            _ => item is not BackpackWeaponItemView and not BackpackReliquaryItemView,
        };
    }
}
