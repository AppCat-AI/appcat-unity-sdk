using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AppCat.Internal;
using NUnit.Framework;

namespace AppCat.Tests
{
    /// <summary>
    /// Wrapper hardening contract, ported from appcat-react-native-sdk
    /// src/__tests__/index.test.ts. Runs in Unity EditMode and via dotnet.
    /// </summary>
    [TestFixture]
    public class AppCatTests
    {
        private FakeCoreBackend _backend;
        private List<Exception> _errors;

        [SetUp]
        public void SetUp()
        {
            AppCat.Reset();
            _backend = new FakeCoreBackend();
            _errors = new List<Exception>();
            AppCat.BackendFactory = () => _backend;
            AppCat.Dispatcher = new InlineDispatcher();
            AppCatLog.IsDevelopment = true;
            AppCatLog.WarnSink = _ => { };
        }

        [TearDown]
        public void TearDown()
        {
            AppCat.Reset();
            AppCat.BackendFactory = null;
            AppCat.Dispatcher = null;
        }

        private AppCatConfig Config(string apiKey = "key", string appId = "app") => new AppCatConfig
        {
            ApiKey = apiKey,
            AppId = appId,
            OnError = e => _errors.Add(e),
        };

        // ------------------------------------------------------------------
        // Init
        // ------------------------------------------------------------------

        [Test]
        public void Init_ThrowsInvalidApiKeyWhenMissing()
        {
            var ex = Assert.ThrowsAsync<AppCatException>(() => AppCat.Init(new AppCatConfig { ApiKey = "" }));
            Assert.That(ex.Code, Is.EqualTo(AppCatErrorCode.InvalidApiKey));
            Assert.That(AppCat.IsInitialized, Is.False);
        }

        [Test]
        public void Init_ThrowsNoBackendWhenNoneAvailable()
        {
            AppCat.BackendFactory = () => null;
            var ex = Assert.ThrowsAsync<AppCatException>(() => AppCat.Init(Config()));
            Assert.That(ex.Code, Is.EqualTo(AppCatErrorCode.NoBackend));
        }

        [Test]
        public async Task Init_ReturnsEmptyResponseWhenNoMatch()
        {
            var res = await AppCat.Init(Config());
            Assert.That(res.DeepLinkParams, Is.Null);
            Assert.That(res.Geo, Is.Null);
            Assert.That(AppCat.IsInitialized, Is.True);
        }

        [Test]
        public async Task Init_MapsDeepLinkParamsAndGeo()
        {
            _backend.ConfigureResult = new Dictionary<string, object>
            {
                ["deepLinkParams"] = new Dictionary<string, object> { ["promo"] = "summer", ["n"] = 1L },
                ["geo"] = new Dictionary<string, object> { ["city"] = "NYC", ["country"] = "US", ["state"] = "NY" },
            };

            var res = await AppCat.Init(Config());

            Assert.That(res.DeepLinkParams["promo"], Is.EqualTo("summer"));
            Assert.That(res.DeepLinkParams.ContainsKey("n"), Is.False, "non-string params are dropped");
            Assert.That(res.Geo.City, Is.EqualTo("NYC"));
            Assert.That(res.Geo.State, Is.EqualTo("NY"));
        }

        [Test]
        public async Task Init_PassesCredentialsAndOptionsToBackend()
        {
            await AppCat.Init(new AppCatConfig
            {
                ApiKey = "k",
                AppId = "a",
                IsDebug = true,
                LogLevel = AppCatLogLevel.Debug,
                CustomerUserId = "u1",
            });

            Assert.That(_backend.LastApiKey, Is.EqualTo("k"));
            Assert.That(_backend.LastAppId, Is.EqualTo("a"));
            Assert.That(_backend.LastConfigureOptions["isDebug"], Is.EqualTo(true));
            Assert.That(_backend.LastConfigureOptions["logLevel"], Is.EqualTo(0));
            Assert.That(_backend.LastConfigureOptions["customerUserId"], Is.EqualTo("u1"));
            Assert.That(_backend.LastLogLevel, Is.EqualTo(0));
        }

        [Test]
        public async Task Init_SendsEmptyAppIdWhenOmitted()
        {
            await AppCat.Init(new AppCatConfig { ApiKey = "k" });
            Assert.That(_backend.LastAppId, Is.EqualTo(string.Empty));
        }

        [Test]
        public async Task Init_IsIdempotent()
        {
            await AppCat.Init(Config());
            var second = await AppCat.Init(Config());
            Assert.That(second.DeepLinkParams, Is.Null);
            Assert.That(_backend.Calls.FindAll(c => c == "configure").Count, Is.EqualTo(1));
        }

        [Test]
        public void Init_RethrowsTypedBackendErrors()
        {
            _backend.ConfigureThrows = new AppCatException(AppCatErrorCode.ConfigureFailed, "Failed to fetch SDK config");
            var ex = Assert.ThrowsAsync<AppCatException>(() => AppCat.Init(Config()));
            Assert.That(ex.Code, Is.EqualTo(AppCatErrorCode.ConfigureFailed));
            Assert.That(ex.Message, Does.Contain("Failed to fetch SDK config"));
        }

        [Test]
        public void Init_WrapsUnknownBackendErrorsAsConfigureFailed()
        {
            _backend.ConfigureThrows = new InvalidOperationException("boom");
            var ex = Assert.ThrowsAsync<AppCatException>(() => AppCat.Init(Config()));
            Assert.That(ex.Code, Is.EqualTo(AppCatErrorCode.ConfigureFailed));
            Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(AppCat.IsInitialized, Is.False);
        }

        [Test]
        public async Task Init_ReportsNativeBackendFlag()
        {
            _backend.IsNative = true;
            await AppCat.Init(Config());
            Assert.That(AppCat.IsNativeBackend, Is.True);
        }

        [Test]
        public void Init_CallbackOverloadDeliversResult()
        {
            InitResponse got = null;
            AppCatException err = null;
            AppCat.Init(Config(), r => got = r, e => err = e);
            SpinUntil(() => got != null || err != null);
            Assert.That(err, Is.Null);
            Assert.That(got, Is.Not.Null);
        }

        [Test]
        public void Init_CallbackOverloadDeliversTypedError()
        {
            AppCatException err = null;
            AppCat.Init(new AppCatConfig { ApiKey = "" }, _ => { }, e => err = e);
            SpinUntil(() => err != null);
            Assert.That(err.Code, Is.EqualTo(AppCatErrorCode.InvalidApiKey));
        }

        // ------------------------------------------------------------------
        // Before init: every best-effort method returns its fallback
        // ------------------------------------------------------------------

        [Test]
        public async Task BeforeInit_BestEffortMethodsReturnFallbacksAndReportNotConfigured()
        {
            AppCat.Reset();
            // OnError is only registered through Init; verify fallbacks and no throws.
            Assert.That(await AppCat.Identify(new IdentifyData { UserId = "u" }), Is.Null);
            Assert.DoesNotThrow(() => AppCat.SendEvent("Purchase"));
            Assert.That(await AppCat.GetAttribution(), Is.Null);
            Assert.That(await AppCat.GetDeviceContext(), Is.Null);
            Assert.That(await AppCat.GetAppCatId(), Is.EqualTo(string.Empty));
            Assert.That(await AppCat.IsDisabled(), Is.False);
            Assert.DoesNotThrowAsync(() => AppCat.SetTrackingConsent(false));
            Assert.That(_backend.Calls, Is.Empty);
        }

        // ------------------------------------------------------------------
        // SendEvent
        // ------------------------------------------------------------------

        [Test]
        public async Task SendEvent_SplitsReservedKeysIntoOptions()
        {
            await AppCat.Init(Config());

            AppCat.SendEvent("Purchase", new Dictionary<string, object>
            {
                ["orderId"] = "ord_1",
                ["value"] = 9.99,
                ["currency"] = "USD",
                ["eventId"] = "purchase_ord_1",
                ["testEventCode"] = "TEST123",
                ["skipped"] = null,
            });

            Assert.That(_backend.LastEventName, Is.EqualTo("Purchase"));
            Assert.That(_backend.LastEventParams.Keys, Is.EquivalentTo(new[] { "orderId" }));
            Assert.That(_backend.LastEventOptions["value"], Is.EqualTo(9.99));
            Assert.That(_backend.LastEventOptions["currency"], Is.EqualTo("USD"));
            Assert.That(_backend.LastEventOptions["eventId"], Is.EqualTo("purchase_ord_1"));
            Assert.That(_backend.LastEventOptions["testEventCode"], Is.EqualTo("TEST123"));
        }

        [Test]
        public async Task SendEvent_WithoutParamsSendsEmptyDictionaries()
        {
            await AppCat.Init(Config());
            AppCat.SendEvent("ViewContent");
            Assert.That(_backend.LastEventParams, Is.Empty);
            Assert.That(_backend.LastEventOptions, Is.Empty);
        }

        [Test]
        public async Task SendEvent_WarnsWhenRevenueEventMissingValueOrCurrency()
        {
            var warnings = new List<string>();
            AppCatLog.WarnSink = warnings.Add;
            await AppCat.Init(Config());

            AppCat.SendEvent("Purchase", new Dictionary<string, object> { ["value"] = 1.0 });
            AppCat.SendEvent("InitiateCheckout");
            AppCat.SendEvent("ViewContent");
            AppCat.SendEvent("Purchase", new Dictionary<string, object> { ["value"] = 1.0, ["currency"] = "USD" });

            Assert.That(warnings.Count, Is.EqualTo(2));
            Assert.That(warnings[0], Does.Contain("Purchase"));
            Assert.That(warnings[1], Does.Contain("InitiateCheckout"));
        }

        [Test]
        public async Task SendEvent_SwallowsBackendErrorsAndReports()
        {
            await AppCat.Init(Config());
            _backend.Throws = new InvalidOperationException("native boom");

            Assert.DoesNotThrow(() => AppCat.SendEvent("Purchase"));
            Assert.That(_errors.Count, Is.EqualTo(1));
            Assert.That(_errors[0].Message, Is.EqualTo("native boom"));
        }

        // ------------------------------------------------------------------
        // Identify
        // ------------------------------------------------------------------

        [Test]
        public async Task Identify_FlattensDataAndMapsResponse()
        {
            await AppCat.Init(Config());
            _backend.IdentifyResult = new Dictionary<string, object>
            {
                ["geo"] = new Dictionary<string, object> { ["city"] = "Austin", ["country"] = "US", ["state"] = "TX" },
                ["deepLinkParams"] = new Dictionary<string, object> { ["ref"] = "tiktok" },
            };

            var res = await AppCat.Identify(new IdentifyData
            {
                UserId = "u1",
                Email = "a@b.com",
                RevenueCatIds = new[] { "rc_1" },
                CustomAttributes = new Dictionary<string, object> { ["tier"] = "gold" },
            });

            Assert.That(_backend.LastIdentifyData["userId"], Is.EqualTo("u1"));
            Assert.That(_backend.LastIdentifyData["email"], Is.EqualTo("a@b.com"));
            Assert.That(_backend.LastIdentifyData.ContainsKey("phone"), Is.False);
            Assert.That(((List<object>)_backend.LastIdentifyData["revenueCatIds"])[0], Is.EqualTo("rc_1"));
            Assert.That(((Dictionary<string, object>)_backend.LastIdentifyData["customAttributes"])["tier"], Is.EqualTo("gold"));
            Assert.That(res.Geo.City, Is.EqualTo("Austin"));
            Assert.That(res.DeepLinkParams["ref"], Is.EqualTo("tiktok"));
        }

        [Test]
        public async Task Identify_ReturnsEmptyResponseWhenBackendReturnsNull()
        {
            await AppCat.Init(Config());
            _backend.IdentifyResult = null;
            var res = await AppCat.Identify(new IdentifyData { UserId = "u1" });
            Assert.That(res, Is.Not.Null);
            Assert.That(res.Geo, Is.Null);
            Assert.That(res.DeepLinkParams, Is.Null);
        }

        [Test]
        public async Task Identify_ReturnsNullAndReportsOnError()
        {
            await AppCat.Init(Config());
            _backend.Throws = new InvalidOperationException("boom");
            var res = await AppCat.Identify(new IdentifyData { UserId = "u1" });
            Assert.That(res, Is.Null);
            Assert.That(_errors.Count, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------
        // Getters / consent / reset
        // ------------------------------------------------------------------

        [Test]
        public async Task Getters_DelegateToBackend()
        {
            await AppCat.Init(Config());
            Assert.That((await AppCat.GetAttribution())["fbc"], Is.EqualTo("fb.1.1"));
            Assert.That((await AppCat.GetDeviceContext())["platform"], Is.EqualTo("ios"));
            Assert.That(await AppCat.GetAppCatId(), Is.EqualTo("device-1"));
            Assert.That(await AppCat.IsDisabled(), Is.True);
        }

        [Test]
        public async Task Getters_ReturnFallbacksOnError()
        {
            await AppCat.Init(Config());
            _backend.Throws = new InvalidOperationException("boom");
            Assert.That(await AppCat.GetAttribution(), Is.Null);
            Assert.That(await AppCat.GetDeviceContext(), Is.Null);
            Assert.That(await AppCat.GetAppCatId(), Is.EqualTo(string.Empty));
            Assert.That(await AppCat.IsDisabled(), Is.False);
            Assert.That(_errors.Count, Is.EqualTo(4));
        }

        [Test]
        public async Task SetTrackingConsent_ForwardsFlagAndSwallowsErrors()
        {
            await AppCat.Init(Config());
            await AppCat.SetTrackingConsent(false);
            Assert.That(_backend.LastConsent, Is.False);

            _backend.Throws = new InvalidOperationException("boom");
            Assert.DoesNotThrowAsync(() => AppCat.SetTrackingConsent(true));
            Assert.That(_errors.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task OnError_HostCallbackThrowingIsContained()
        {
            await AppCat.Init(new AppCatConfig
            {
                ApiKey = "k",
                OnError = _ => throw new Exception("host bug"),
            });
            _backend.Throws = new InvalidOperationException("boom");
            Assert.DoesNotThrow(() => AppCat.SendEvent("ViewContent"));
        }

        [Test]
        public async Task Reset_ClearsState()
        {
            await AppCat.Init(Config());
            AppCat.Reset();
            Assert.That(AppCat.IsInitialized, Is.False);
            Assert.That(AppCat.IsNativeBackend, Is.False);
        }

        private static void SpinUntil(Func<bool> condition, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!condition() && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(5);
        }
    }
}
