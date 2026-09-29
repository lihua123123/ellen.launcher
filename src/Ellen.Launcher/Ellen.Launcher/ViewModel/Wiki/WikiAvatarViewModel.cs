// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.Calculable;
using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Service.Combat;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.Service.Cultivation.Consumption;
using Ellen.Launcher.Service.Cultivation.Offline;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using System.Collections.Immutable;
using CalculateBatchConsumption = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.BatchConsumption;

namespace Ellen.Launcher.ViewModel.Wiki;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class WikiAvatarViewModel : Abstraction.ViewModel
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ICultivationService cultivationService;
    private readonly IAvatarCombatNoteService avatarCombatNoteService;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    private WikiAvatarMetadataContext? metadataContext;

    [GeneratedConstructor]
    public partial WikiAvatarViewModel(IServiceProvider serviceProvider);

    public IAdvancedCollectionView<Avatar>? Avatars
    {
        get;
        set
        {
            AdvancedCollectionViewCurrentChanged.Detach(field, OnCurrentAvatarChanged);
            SetProperty(ref field, value);
            AdvancedCollectionViewCurrentChanged.Attach(field, OnCurrentAvatarChanged);
        }
    }

    [ObservableProperty]
    public partial BaseValueInfo? BaseValueInfo { get; set; }

    [ObservableProperty]
    public partial SearchData? SearchData { get; set; }

    [ObservableProperty]
    public partial LinkMetadataContext? LinkContext { get; set; }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        if (!await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        metadataContext = await metadataService.GetContextAsync<WikiAvatarMetadataContext>(token).ConfigureAwait(false);
        ImmutableArray<Avatar> avatars = [.. metadataContext.Avatars.OrderByDescending(avatar => avatar.BeginTime).ThenByDescending(avatar => avatar.Sort)];
        SearchData searchData = SearchData.CreateForWikiAvatar(avatars);
        await CombineComplexDataAsync(avatars, metadataContext).ConfigureAwait(false);

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            IAdvancedCollectionView<Avatar> avatarsView = avatars.AsAdvancedCollectionView();

            await taskContext.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();

            SearchData = searchData;
            Avatars = avatarsView;
            Avatars.MoveCurrentToFirst();
        }

        return true;
    }

    private void OnCurrentAvatarChanged(object? sender, object? e)
    {
        UpdateBaseValueInfo(Avatars?.CurrentItem);
        UpdateLinkContext(Avatars?.CurrentItem);
        Avatars?.CurrentItem?.CostumesView?.MoveCurrentToFirst();
    }

    private ValueTask CombineComplexDataAsync(ImmutableArray<Avatar> avatars, WikiAvatarMetadataContext context)
    {
        foreach (Avatar avatar in avatars)
        {
            avatar.CookBonusView ??= CookBonusView.Create(avatar.FetterInfo.CookBonus, context.IdMaterialMap);
            avatar.CultivationItemsView ??= [.. avatar.CultivationItems.Select(i => context.IdMaterialMap.GetValueOrDefault(i, Material.Default))];
            avatar.CostumesView ??= avatar.Costumes.OrderByDescending(c => c.IsDefault).AsAdvancedCollectionView();

            // 「元素附着及产球」备注表：纯附加信息，未收录的角色不会产生任何可见影响。
            avatarCombatNoteService.ApplyTo(avatar);
        }

        return ValueTask.CompletedTask;
    }

    [Command("CultivateCommand")]
    private async Task CultivateAsync(Avatar? avatar)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Cultivate", "WikiAvatarViewModel.Command"));

        if (avatar is null)
        {
            return;
        }

        CultivatePromotionDeltaOptions deltaOptions;
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            CalculableOptions options = new(avatar.ToCalculable(), null);
            CultivatePromotionDeltaDialog dialog = await contentDialogFactory.CreateInstanceAsync<CultivatePromotionDeltaDialog>(scope.ServiceProvider, options).ConfigureAwait(false);
            (bool isOk, deltaOptions) = await dialog.GetPromotionDeltaAsync().ConfigureAwait(false);

            if (!isOk)
            {
                return;
            }
        }

        CalculateBatchConsumption batchConsumption = OfflineCalculator.CalculateWikiAvatarConsumption(deltaOptions.Delta, avatar);
        if (batchConsumption.OverallConsume.IsEmpty)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddNoConsumptionWarning));
            return;
        }

        LevelInformation levelInformation = LevelInformation.From(deltaOptions.Delta);
        try
        {
            InputConsumption input = new()
            {
                Type = CultivateType.AvatarAndSkill,
                ItemId = avatar.Id,
                Items = batchConsumption.OverallConsume,
                LevelInformation = levelInformation,
                Strategy = deltaOptions.Strategy,
            };

            InfoBarMessage? message = await cultivationService.SaveConsumptionAsync(input).ConfigureAwait(false) switch
            {
                ConsumptionSaveResultKind.NoProject => InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddWarning),
                ConsumptionSaveResultKind.Skipped => InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveSkippedHint),
                ConsumptionSaveResultKind.NoItem => InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveNoItemHint),
                ConsumptionSaveResultKind.Added => InfoBarMessage.Success(SH.ViewModelCultivationEntryAddSuccess),
                _ => default,
            };

            if (message is not null)
            {
                messenger.Send(message);
            }
        }
        catch (EllenException ex)
        {
            messenger.Send(InfoBarMessage.Error(SH.ViewModelCultivationAddWarning, ex));
        }
    }

    private void UpdateBaseValueInfo(Avatar? avatar)
    {
        if (avatar is null || metadataContext is null)
        {
            BaseValueInfo = null;
            return;
        }

        BaseValueInfoMetadataContext context = new()
        {
            GrowCurveMap = metadataContext.LevelDictionaryAvatarGrowCurveMap,
            PromoteMap = metadataContext.IdDictionaryAvatarLevelPromoteMap[avatar.PromoteId],
        };

        BaseValueInfo = new(avatar.MaxLevel, avatar.GrowCurves.ToPropertyCurveValues(avatar.BaseValue), context);
    }

    private void UpdateLinkContext(Avatar? avatar)
    {
        if (avatar is null || metadataContext is null)
        {
            LinkContext = null;
            return;
        }

        LinkContext = new()
        {
            IdNameMap = metadataContext.IdHyperLinkNameMap,
            Inherents = avatar.SkillDepot.Inherents,
            Skills = avatar.SkillDepot.CompositeSkillsNoInherents,
            Talents = avatar.SkillDepot.Talents,
        };
    }

    [Command("FilterCommand")]
    private void ApplyFilter()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Filter avatars", "WikiAvatarViewModel.Command"));

        if (Avatars is null)
        {
            return;
        }

        Avatars.Filter = AvatarFilter.Compile(SearchData);

        if (Avatars.CurrentItem is null)
        {
            Avatars.MoveCurrentToFirst();
        }
    }
}
