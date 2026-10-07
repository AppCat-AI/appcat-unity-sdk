using System.Collections.Generic;
using System.Threading.Tasks;
using AppCat.Internal;
using NUnit.Framework;

namespace AppCat.Tests
{
    [TestFixture]
    public class EventParamSplitterTests
    {
        [Test]
        public void SplitsReservedKeysAndDropsNulls()
        {
            EventParamSplitter.Split(new Dictionary<string, object>
            {
                ["a"] = 1,
                ["eventId"] = "e",
                ["value"] = 2.5,
                ["currency"] = "USD",
                ["testEventCode"] = "T",
                ["nil"] = null,
            }, out var p, out var o);

            Assert.That(p.Keys, Is.EquivalentTo(new[] { "a" }));
            Assert.That(o.Keys, Is.EquivalentTo(new[] { "eventId", "value", "currency", "testEventCode" }));
        }

        [Test]
        public void NullInputYieldsEmptyDictionaries()
        {
            EventParamSplitter.Split(null, out var p, out var o);
            Assert.That(p, Is.Empty);
            Assert.That(o, Is.Empty);
        }

        [Test]
        public void DetectsMissingRevenueFieldsOnlyForRevenueEvents()
        {
            Assert.That(EventParamSplitter.IsMissingRevenueFields("Purchase", null), Is.True);
            Assert.That(EventParamSplitter.IsMissingRevenueFields("InitiateCheckout", new Dictionary<string, object> { ["value"] = 1 }), Is.True);
            Assert.That(EventParamSplitter.IsMissingRevenueFields("Purchase", new Dictionary<string, object> { ["value"] = 1, ["currency"] = "USD" }), Is.False);
            Assert.That(EventParamSplitter.IsMissingRevenueFields("ViewContent", null), Is.False);
        }
    }

    [TestFixture]
    public class AppCatJsonTests
    {
        [Test]
        public void RoundTrips()
        {
            var text = AppCatJson.Encode(new Dictionary<string, object>
            {
                ["s"] = "x\"y",
                ["n"] = 3,
                ["d"] = 1.5,
                ["b"] = false,
                ["z"] = null,
                ["l"] = new List<object> { "a", 2 },
                ["o"] = new Dictionary<string, object> { ["k"] = "v" },
            });
            var back = AppCatJson.DecodeObject(text);
            Assert.That(back["s"], Is.EqualTo("x\"y"));
            Assert.That(back["n"], Is.EqualTo(3L));
            Assert.That(back["d"], Is.EqualTo(1.5));
            Assert.That(back["b"], Is.EqualTo(false));
            Assert.That(back["z"], Is.Null);
            Assert.That(((List<object>)back["l"])[1], Is.EqualTo(2L));
            Assert.That(((Dictionary<string, object>)back["o"])["k"], Is.EqualTo("v"));
        }

        [Test]
        public void MalformedInputReturnsNull()
        {
            Assert.That(AppCatJson.Decode("{"), Is.Null);
            Assert.That(AppCatJson.Decode(""), Is.Null);
            Assert.That(AppCatJson.Decode(null), Is.Null);
            Assert.That(AppCatJson.DecodeObject("[]"), Is.Null);
        }

        [Test]
        public void EncodesNullAndNaNSafely()
        {
            Assert.That(AppCatJson.Encode(null), Is.EqualTo("null"));
            Assert.That(AppCatJson.Encode(double.NaN), Is.EqualTo("null"));
        }
    }

    [TestFixture]
    public class ResponseMapperTests
    {
        [Test]
        public void NullAndEmptyMapToEmptyResponses()
        {
            Assert.That(ResponseMapper.ToInitResponse(null).DeepLinkParams, Is.Null);
            var r = ResponseMapper.ToInitResponse(new Dictionary<string, object>
            {
                ["deepLinkParams"] = new Dictionary<string, object>(),
                ["geo"] = new Dictionary<string, object>(),
            });
            Assert.That(r.DeepLinkParams, Is.Null, "empty params collapse to null");
            Assert.That(r.Geo, Is.Null, "geo with no fields collapses to null");
        }

        [Test]
        public void GeoWithPartialFieldsIsKept()
        {
            var geo = ResponseMapper.Geo(new Dictionary<string, object> { ["country"] = "US" });
            Assert.That(geo.Country, Is.EqualTo("US"));
            Assert.That(geo.City, Is.Null);
        }
    }

    [TestFixture]
    public class NativeCallbackRegistryTests
    {
        [Test]
        public async Task CompletesRegisteredRequestWithJson()
        {
            var reg = new NativeCallbackRegistry(new InlineDispatcher());
            var id = reg.Register(out var task);
            reg.Complete(id, "{\"a\":1}", null, null);
            Assert.That(await task, Is.EqualTo("{\"a\":1}"));
            Assert.That(reg.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void FailsWithTypedExceptionFromNativeCode()
        {
            var reg = new NativeCallbackRegistry(new InlineDispatcher());
            var id = reg.Register(out var task);
            reg.Complete(id, null, "ERR_NOT_CONFIGURED", "Call configure() first");
            var ex = Assert.ThrowsAsync<AppCatException>(async () => await task);
            Assert.That(ex.Code, Is.EqualTo(AppCatErrorCode.NotConfigured));
            Assert.That(ex.Message, Is.EqualTo("Call configure() first"));
        }

        [Test]
        public void IgnoresUnknownAndDuplicateIds()
        {
            var reg = new NativeCallbackRegistry(new InlineDispatcher());
            var id = reg.Register(out var task);
            reg.Complete(id, "1", null, null);
            Assert.DoesNotThrow(() => reg.Complete(id, "2", null, null));
            Assert.DoesNotThrow(() => reg.Complete(9999, "x", null, null));
            Assert.That(task.Result, Is.EqualTo("1"));
        }

        [Test]
        public void IdsAreUniqueAndFailAllRejectsPending()
        {
            var reg = new NativeCallbackRegistry(new InlineDispatcher());
            var a = reg.Register(out var ta);
            var b = reg.Register(out var tb);
            Assert.That(a, Is.Not.EqualTo(b));
            reg.FailAll("shutdown");
            Assert.ThrowsAsync<AppCatException>(async () => await ta);
            Assert.ThrowsAsync<AppCatException>(async () => await tb);
            Assert.That(reg.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void CompletionIsMarshalledThroughDispatcher()
        {
            var posted = new List<System.Action>();
            var reg = new NativeCallbackRegistry(new RecordingDispatcher(posted));
            var id = reg.Register(out var task);
            reg.Complete(id, "{}", null, null);
            Assert.That(task.IsCompleted, Is.False, "must not complete before the dispatcher runs");
            foreach (var a in posted) a();
            Assert.That(task.IsCompleted, Is.True);
        }

        private sealed class RecordingDispatcher : IDispatcher
        {
            private readonly List<System.Action> _posted;
            public RecordingDispatcher(List<System.Action> posted) { _posted = posted; }
            public void Post(System.Action action) => _posted.Add(action);
        }
    }

    [TestFixture]
    public class ErrorCodeMappingTests
    {
        [TestCase("ERR_INVALID_CONFIG", AppCatErrorCode.InvalidConfig)]
        [TestCase("INVALID_API_KEY", AppCatErrorCode.InvalidApiKey)]
        [TestCase("ERR_NOT_CONFIGURED", AppCatErrorCode.NotConfigured)]
        [TestCase("NOT_CONFIGURED", AppCatErrorCode.NotConfigured)]
        [TestCase("ERR_SERIALIZE", AppCatErrorCode.SerializeFailed)]
        [TestCase("ERR_SET_TRACKING_CONSENT", AppCatErrorCode.SetTrackingConsentFailed)]
        [TestCase("CONFIG_FETCH_FAILED", AppCatErrorCode.ConfigureFailed)]
        [TestCase("ERR_SOMETHING_ELSE", AppCatErrorCode.Internal)]
        [TestCase(null, AppCatErrorCode.Internal)]
        public void MapsNativeCodes(string native, AppCatErrorCode expected)
        {
            Assert.That(AppCatException.CodeFromNative(native), Is.EqualTo(expected));
        }
    }

    [TestFixture]
    public class IdentifyDataTests
    {
        [Test]
        public void OmitsEmptyFields()
        {
            var d = new IdentifyData { UserId = "u", Email = "" }.ToDictionary();
            Assert.That(d.Keys, Is.EquivalentTo(new[] { "userId" }));
        }
    }
}
