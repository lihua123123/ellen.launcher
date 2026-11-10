using System.Net.Http;

namespace Ellen.Launcher.Test.BaseClassLibrary;

[TestClass]
public sealed class HttpClientTest
{
    /// <summary>真实请求 api.snaphutaorp.org，网络不通即超时；已登记为 known-env-failure。</summary>
    [TestMethod]
    [TestCategory("KnownEnvFailure")]
    public void RedirectionHeaderTest()
    {
        HttpClientHandler handler = new()
        {
            UseCookies = false,
            AllowAutoRedirect = false,
        };

        using (handler)
        {
            using (HttpClient httpClient = new(handler))
            {
                using (HttpRequestMessage request = new(HttpMethod.Get, "https://api.snaphutaorp.org/patch/hutao/download"))
                {
                    using (HttpResponseMessage response = httpClient.Send(request))
                    {
                        _ = response;
                    }
                }
            }
        }
    }
}