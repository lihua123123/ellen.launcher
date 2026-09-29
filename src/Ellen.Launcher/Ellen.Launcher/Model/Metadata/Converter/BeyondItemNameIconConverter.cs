using Ellen.Launcher.Web.Endpoint.Upstream;

namespace Ellen.Launcher.Model.Metadata.Converter;

public static class BeyondItemNameIconConverter
{

    public static Uri IconNameToUri(string name)
    {
        return StaticResourcesEndpoints.StaticRaw("BeyondItemIcon", $"{name}.png").ToUri();
    }
}
