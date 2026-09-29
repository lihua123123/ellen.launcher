// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Response;

public static class ResponseValidationServiceCollectionExtension
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddResponseValidation()
        {
            return services
                .AddTransient<ICommonResponseValidator<Response>, DefaultResponseValidator>()
                .AddTransient(typeof(ITypedResponseValidator<>), typeof(TypedResponseValidator<>));
        }
    }
}