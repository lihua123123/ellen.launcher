// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Web.Enka;
using Ellen.Launcher.Web.Enka.Model;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;

namespace Ellen.Launcher.Service.User;

[Service(ServiceLifetime.Singleton, typeof(IProfilePictureService))]
public sealed partial class ProfilePictureService : IProfilePictureService
{
    private readonly IUidProfilePictureRepository uidProfilePictureRepository;
    private readonly IMetadataService metadataService;
    private readonly IServiceProvider serviceProvider;
    private readonly ITaskContext taskContext;

    private readonly Lock syncRoot = new();

    [GeneratedConstructor]
    public partial ProfilePictureService(IServiceProvider serviceProvider);

    public async ValueTask TryInitializeAsync(ViewModel.User.User user, CancellationToken token = default)
    {
        foreach (UserGameRole userGameRole in user.UserGameRoles)
        {
            UidProfilePicture? profilePicture;
            lock (syncRoot)
            {
                profilePicture = uidProfilePictureRepository.SingleUidProfilePictureOrDefaultByUid(userGameRole.GameUid);
            }

            if (profilePicture is not null)
            {
                if (await TryAttachProfilePictureToUserGameRoleAsync(userGameRole, profilePicture, token).ConfigureAwait(false))
                {
                    continue;
                }
            }

            // Force update
            await RefreshUserGameRoleAsync(userGameRole, token).ConfigureAwait(false);
        }
    }

    public async ValueTask RefreshUserGameRoleAsync(UserGameRole userGameRole, CancellationToken token = default)
    {
        EnkaResponse? enkaResponse;
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            EnkaClient enkaClient = scope.ServiceProvider.GetRequiredService<EnkaClient>();

            enkaResponse =
                await enkaClient.GetForwardPlayerInfoAsync(userGameRole, token).ConfigureAwait(false) ??
                await enkaClient.GetPlayerInfoAsync(userGameRole, token).ConfigureAwait(false);
        }

        if (enkaResponse is { PlayerInfo.ProfilePicture: { } innerProfilePicture })
        {
            UidProfilePicture profilePicture = UidProfilePicture.From(userGameRole, innerProfilePicture);

            // We don't use DbTransaction here because it's rather complicated
            // to handle transaction over multiple DbContext
            lock (syncRoot)
            {
                uidProfilePictureRepository.DeleteUidProfilePictureByUid(userGameRole.GameUid);
                uidProfilePictureRepository.UpdateUidProfilePicture(profilePicture);
            }

            await TryAttachProfilePictureToUserGameRoleAsync(userGameRole, profilePicture, token).ConfigureAwait(false);
        }
    }

    private async ValueTask<bool> TryAttachProfilePictureToUserGameRoleAsync(UserGameRole userGameRole, UidProfilePicture cache, CancellationToken token = default)
    {
        if (cache.RefreshTime.AddDays(15) < DateTimeOffset.Now)
        {
            return false;
        }

        if (!await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        UserMetadataContext context = await metadataService.GetContextAsync<UserMetadataContext>(token).ConfigureAwait(false);

        await taskContext.SwitchToMainThreadAsync();

        // Most common to most rare
        if (cache.ProfilePictureId is not 0U)
        {
            userGameRole.ProfilePictureIcon = context.ProfilePictures
                .Single(p => p.Id == cache.ProfilePictureId)
                .Icon;

            return true;
        }

        if (cache.AvatarId is not 0U)
        {
            userGameRole.ProfilePictureIcon = context.ProfilePictures
                .Single(p => p.UnlockType is ProfilePictureUnlockBy.Avatar && p.UnlockParameter == cache.AvatarId)
                .Icon;

            return true;
        }

        if (cache.CostumeId is not 0U)
        {
            userGameRole.ProfilePictureIcon = context.ProfilePictures
                .Single(p => p.UnlockType is ProfilePictureUnlockBy.Costume && p.UnlockParameter == cache.CostumeId)
                .Icon;

            return true;
        }

        return false;
    }
}