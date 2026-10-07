using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppCat.Core;
using UnityEngine.Networking;

namespace AppCat.Internal
{
    /// <summary>
    /// UnityWebRequest-backed transport for the managed core. Required on
    /// WebGL (no sockets / threads) and safe everywhere else. Completion is
    /// observed through the request's async operation, so no coroutine host
    /// is needed.
    /// </summary>
    internal sealed class UnityWebRequestTransport : IHttpTransport
    {
        public Task<HttpResponse> SendAsync(
            string method,
            string url,
            IReadOnlyDictionary<string, string> headers,
            string body,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<HttpResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            var req = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
            };
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body))
                {
                    contentType = "application/json",
                };
            }
            if (headers != null)
            {
                foreach (var kv in headers) req.SetRequestHeader(kv.Key, kv.Value);
            }

            var registration = cancellationToken.Register(() =>
            {
                try { req.Abort(); } catch { /* already finished */ }
            });

            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                registration.Dispose();
                try
                {
                    var status = (int)req.responseCode;
                    var text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                    if (status == 0 && req.result != UnityWebRequest.Result.Success)
                    {
                        tcs.TrySetException(new System.Net.WebException(req.error ?? "request failed"));
                    }
                    else
                    {
                        tcs.TrySetResult(new HttpResponse(status, text));
                    }
                }
                finally
                {
                    req.Dispose();
                }
            };

            return tcs.Task;
        }
    }
}
