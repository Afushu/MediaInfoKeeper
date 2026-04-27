namespace MediaInfoKeeper.Services
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Emby.Web.GenericEdit.Elements;

    internal static class ProxyLatencyProbe
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
        private const string ProbeUrl1 = "http://www.gstatic.com/generate_204";
        private const string ProbeUrl2 = "http://www.google.com/generate_204";

        internal sealed class Result
        {
            public ItemStatus Status { get; set; }

            public string Caption { get; set; }

            public string StatusText { get; set; }
        }

        public static Task<Result> RunAsync(Options.NetWorkOptions options)
        {
            if (options == null)
            {
                return Task.FromResult(Build(ItemStatus.Unavailable, "不可用", "N/A"));
            }

            if (!options.EnableProxyServer)
            {
                return Task.FromResult(Build(ItemStatus.Unavailable, "不可用", "N/A"));
            }

            if (!TryParseProxyEndpoint(options.ProxyServerUrl, out var scheme, out var host, out var port, out var username, out var password))
            {
                return Task.FromResult(Build(ItemStatus.Unavailable, "不可用", "N/A"));
            }

            return ProbeCoreAsync(scheme, host, port, username, password, options.IgnoreCertificateValidation);
        }

        private static async Task<Result> ProbeCoreAsync(
            string scheme,
            string host,
            int port,
            string username,
            string password,
            bool ignoreCertificateValidation)
        {
            try
            {
                var proxyUrl = new UriBuilder(scheme, host, port).Uri;
                using var handler = new HttpClientHandler();
                handler.Proxy = new WebProxy(proxyUrl)
                {
                    Credentials = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)
                        ? new NetworkCredential(username, password)
                        : null
                };
                handler.UseProxy = true;
                if (ignoreCertificateValidation)
                {
                    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                }

                using var client = new HttpClient(handler)
                {
                    Timeout = DefaultTimeout
                };

                var task1 = client.GetAsync(ProbeUrl1);
                var task2 = client.GetAsync(ProbeUrl2);

                var stopwatch = Stopwatch.StartNew();
                var completedTask = await Task.WhenAny(task1, task2).ConfigureAwait(false);
                stopwatch.Stop();

                if (completedTask.Status == TaskStatus.RanToCompletion &&
                    completedTask.Result.IsSuccessStatusCode &&
                    completedTask.Result.StatusCode == HttpStatusCode.NoContent)
                {
                    return Build(
                        ItemStatus.Succeeded,
                        "可用",
                        string.Format(CultureInfo.InvariantCulture, "{0} ms", stopwatch.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)));
                }

                var otherTask = completedTask == task1 ? task2 : task1;
                if (otherTask.Status == TaskStatus.RanToCompletion &&
                    otherTask.Result.IsSuccessStatusCode &&
                    otherTask.Result.StatusCode == HttpStatusCode.NoContent)
                {
                    return Build(
                        ItemStatus.Succeeded,
                        "可用",
                        string.Format(CultureInfo.InvariantCulture, "{0} ms", stopwatch.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
            }

            return Build(ItemStatus.Unavailable, "不可用", "N/A");
        }

        private static Result Build(ItemStatus status, string caption, string statusText)
        {
            return new Result
            {
                Status = status,
                Caption = caption,
                StatusText = statusText
            };
        }

        private static bool TryParseProxyEndpoint(
            string raw,
            out string scheme,
            out string host,
            out int port,
            out string username,
            out string password)
        {
            scheme = null;
            host = null;
            port = 0;
            username = string.Empty;
            password = string.Empty;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri) ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            scheme = uri.Scheme;
            host = uri.Host;
            port = uri.IsDefaultPort ? (uri.Scheme == Uri.UriSchemeHttp ? 80 : 443) : uri.Port;
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userInfoParts = uri.UserInfo.Split(new[] { ':' }, 2);
                username = userInfoParts[0];
                password = userInfoParts.Length > 1 ? userInfoParts[1] : string.Empty;
            }

            return port > 0;
        }
    }
}
