using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppCat.Core;
using AppCat.Internal;
using NUnit.Framework;

namespace AppCat.Tests
{
    /// <summary>
    /// Integration: the full open-source wrapper over the real managed core
    /// (AppCatCore.Unity.dll) with a scripted HTTP transport. Proves the
    /// wire protocol end to end without a network.
    /// </summary>
    [TestFixture]
    public class ManagedCoreBackendTests
    {
        private ScriptedTransport _http;

        [SetUp]
        public void SetUp()
        {
            AppCat.Reset();
            AppCatCore.Reset();
            _http = new ScriptedTransport();
            AppCat.Dispatcher = new InlineDispatcher();
            AppCat.BackendFactory = () => new ManagedCoreBackend(new StubProvider(), _http, (_, __) => { });
        }

        [TearDown]
        public void TearDown()
        {
            AppCat.Reset();
            AppCatCore.Reset();
            AppCatCore.SetHttpTransport(null);
            AppCatCore.SetDeviceContextProvider(null);
            AppCat.BackendFactory = null;
            AppCat.Dispatcher = null;
        }

        [Test]
        public async Task InitResolvesAndMapsThroughTheRealCore()
        {
            _http.Responses.Enqueue("{\"matched\":true,\"deepLinkParams\":{\"promo\":\"summer\"},\"geo\":{\"city\":\"NYC\",\"country\":\"US\",\"state\":\"NY\"},\"enrichment\":{\"fbc\":\"fb.1.1\"}}");

            var res = await AppCat.Init(new AppCatConfig { ApiKey = "key", AppId = "app" });

            Assert.That(AppCat.IsNativeBackend, Is.False);
            Assert.That(res.DeepLinkParams["promo"], Is.EqualTo("summer"));
            Assert.That(res.Geo.City, Is.EqualTo("NYC"));
            Assert.That(_http.Urls[0], Is.EqualTo("https://appcat.ai/api/deferred-deep-link/resolve"));
            Assert.That(_http.Headers[0]["x-api-key"], Is.EqualTo("key"));

            var attribution = await AppCat.GetAttribution();
            Assert.That(attribution["fbc"], Is.EqualTo("fb.1.1"));
            Assert.That(await AppCat.GetAppCatId(), Is.EqualTo("unity-device-1"));
        }

        [Test]
        public void InitSurfacesConfigFetchFailureAsTypedException()
        {
            _http.Responses.Enqueue(null); // 500 for /sdk/config
            var ex = Assert.ThrowsAsync<AppCatException>(() => AppCat.Init(new AppCatConfig { ApiKey = "key" }));
            Assert.That(ex.Code, Is.EqualTo(AppCatErrorCode.ConfigureFailed));
        }

        [Test]
        public async Task SendEventReachesSendAttributeWithSplitPayload()
        {
            // matched:true skips the core's 2s no-match retry.
            _http.Responses.Enqueue("{\"matched\":true}");
            await AppCat.Init(new AppCatConfig { ApiKey = "key", AppId = "app" });
            _http.Responses.Enqueue("{}");

            AppCat.SendEvent("Purchase", new Dictionary<string, object>
            {
                ["orderId"] = "o1",
                ["value"] = 9.99,
                ["currency"] = "USD",
                ["eventId"] = "purchase_o1",
            });
            await WaitFor(() => _http.Urls.Count == 2);

            Assert.That(_http.Urls[1], Does.EndWith("/attribution/device/send-attribute"));
            var body = _http.Bodies[1];
            Assert.That(body, Does.Contain("\"name\":\"Purchase\""));
            Assert.That(body, Does.Contain("\"value\":9.99"));
            Assert.That(body, Does.Contain("\"currency\":\"USD\""));
            Assert.That(body, Does.Contain("\"id\":\"purchase_o1\""));
            Assert.That(body, Does.Contain("\"orderId\":\"o1\""));
            Assert.That(body, Does.Contain("\"platform\":\"editor\""));
        }

        [Test]
        public async Task IdentifyPostsFlattenedIdentity()
        {
            _http.Responses.Enqueue("{\"matched\":true}");
            await AppCat.Init(new AppCatConfig { ApiKey = "key", AppId = "app" });
            _http.Responses.Enqueue("{\"data\":{\"geo\":{\"city\":\"Austin\"}}}");

            var res = await AppCat.Identify(new IdentifyData { UserId = "u1", Email = "a@b.com" });

            Assert.That(_http.Urls[1], Does.EndWith("/attribution/device/identify"));
            Assert.That(_http.Bodies[1], Does.Contain("\"userId\":\"u1\""));
            Assert.That(res.Geo.City, Is.EqualTo("Austin"));
        }

        private static async Task WaitFor(System.Func<bool> cond, int timeoutMs = 2000)
        {
            var deadline = System.DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!cond() && System.DateTime.UtcNow < deadline) await Task.Delay(5);
            Assert.That(cond(), Is.True, "condition not met in time");
        }

        private sealed class StubProvider : IDeviceContextProvider
        {
            public IReadOnlyDictionary<string, object> Collect() => new Dictionary<string, object>
            {
                ["platform"] = "editor",
                ["vendor_id"] = "unity-device-1",
                ["screen_width"] = 1920,
                ["screen_height"] = 1080,
            };
        }

        private sealed class ScriptedTransport : IHttpTransport
        {
            public readonly Queue<string> Responses = new Queue<string>();
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Bodies = new List<string>();
            public readonly List<IReadOnlyDictionary<string, string>> Headers = new List<IReadOnlyDictionary<string, string>>();

            public Task<HttpResponse> SendAsync(string method, string url, IReadOnlyDictionary<string, string> headers, string body, CancellationToken ct)
            {
                lock (Urls)
                {
                    Urls.Add(url);
                    Bodies.Add(body);
                    Headers.Add(headers);
                    var next = Responses.Count > 0 ? Responses.Dequeue() : "{}";
                    return Task.FromResult(next == null ? new HttpResponse(500, "error") : new HttpResponse(200, next));
                }
            }
        }
    }
}
