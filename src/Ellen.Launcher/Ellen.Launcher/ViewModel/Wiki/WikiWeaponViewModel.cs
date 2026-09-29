// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.Calculable;
using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Metadata.Weapon;
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
public sealed partial class WikiWeaponViewModel : Abstraction.ViewModel
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ICultivationService cultivationService;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    private WikiWeaponMetadataContext? metadataContext;

    [GeneratedConstructor]
    public partial WikiWeaponViewModel(IServiceProvider serviceProvider);

    public IAdvancedCollectionView<Weapon>? Weapons
    {
        get;
        set
        {
            AdvancedCollectionViewCurrentChanged.Detach(field, OnCurrentWeaponChanged);
            SetProperty(ref field, value);
            AdvancedCollectionViewCurrentChanged.Attach(field, OnCurrentWeaponChanged);
        }
    }

    [ObservableProperty]
    public partial BaseValueInfo? BaseValueInfo { get; set; }

    [ObservableProperty]
    public partial SearchData? SearchData { get; set; }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        if (!await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        metadataContext = await metadataService.GetContextAsync<WikiWeaponMetadataContext>(token).ConfigureAwait(false);
        ImmutableArray<Weapon> weapons = [.. metadataContext.Weapons.OrderByDescending(weapon => weapon.Sort)];
        SearchData searchData = SearchData.CreateForWikiWeapon(weapons);

        await CombineComplexDataAsync(weapons, metadataContext).ConfigureAwait(false);

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            IAdvancedCollectionView<Weapon> weaponsView = weapons.AsAdvancedCollectionView();

            await taskContext.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();

            SearchData = searchData;
            Weapons = weaponsView;
            Weapons.MoveCurrentToFirst();
        }

        return true;
    }

    private void OnCurrentWeaponChanged(object? sender, object? e)
    {
        UpdateBaseValueInfo(Weapons?.CurrentItem);
    }

    private ValueTask CombineComplexDataAsync(ImmutableArray<Weapon> weapons, WikiWeaponMetadataContext context)
    {
        foreach (Weapon weapon in weapons)
        {
            weapon.CultivationItemsView ??= [.. weapon.CultivationItems.Select(i => context.IdMaterialMap.GetValueOrDefault(i, Material.Default))];
        }

        return ValueTask.CompletedTask;
    }

    [Command("CultivateCommand")]
    private async Task CultivateAsync(Weapon? weapon)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Cultivate", "WikiAvatarViewModel.Command"));

        if (weapon is null)
        {
            return;
        }

        CultivatePromotionDeltaOptions deltaOptions;
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            CalculableOptions options = new(null, weapon.ToCalculable());
            CultivatePromotionDeltaDialog dialog = await contentDialogFactory.CreateInstanceAsync<CultivatePromotionDeltaDialog>(scope.ServiceProvider, options).ConfigureAwait(false);
            (bool isOk, deltaOptions) = await dialog.GetPromotionDeltaAsync().ConfigureAwait(false);

            if (!isOk)
            {
                return;
            }
        }

        CalculateBatchConsumption batchConsumption = OfflineCalculator.CalculateWikiWeaponConsumption(deltaOptions.Delta, weapon);
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
                Type = CultivateType.Weapon,
                ItemId = weapon.Id,
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

    private void UpdateBaseValueInfo(Weapon? weapon)
    {
        if (weapon is null || metadataContext is null)
        {
            BaseValueInfo = null;
            return;
        }

        BaseValueInfoMetadataContext context = new()
        {
            GrowCurveMap = metadataContext.LevelDictionaryWeaponGrowCurveMap,
            PromoteMap = metadataContext.IdDictionaryWeaponLevelPromoteMap[weapon.PromoteId],
        };

        BaseValueInfo = new(weapon.MaxLevel, weapon.GrowCurves.ToPropertyCurveValues(), context);
    }

    [Command("FilterCommand")]
    private void ApplyFilter()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Filter weapons", "WikiAvatarViewModel.Command"));

        if (Weapons is null)
        {
            return;
        }

        Weapons.Filter = WeaponFilter.Compile(SearchData);

        if (Weapons.CurrentItem is null)
        {
            Weapons.MoveCurrentToFirst();
        }
    }
}
