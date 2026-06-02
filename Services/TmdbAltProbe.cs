namespace MediaInfoKeeper.Services
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Threading.Tasks;
    using Emby.Web.GenericEdit.Elements;
    using MediaBrowser.Common.Net;
    using MediaInfoKeeper.Options;

    internal static class TmdbAltProbe
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
        private const string DefaultApiBaseUrl = "https://api.themoviedb.org";
        private const string DefaultImageBaseUrl = "https://image.tmdb.org";
        private const string ImageProbePath = "/t/p/w92/wwemzKWzjKYJFfCeiB57q3r4Bcm.png";

        internal sealed class Result
        {
            public ItemStatus Status { get; set; }

            public string Caption { get; set; }

            public string StatusText { get; set; }
        }

        public static async Task<Result> RunAsync(NetWorkOptions options)
        {
            if (options == null)
            {
                return Build(ItemStatus.Unavailable, "不可用", "N/A");
            }

            var hasApiOverride = !string.IsNullOrWhiteSpace(options.AlternativeTmdbApiUrl) ||
                                 !string.IsNullOrWhiteSpace(options.AlternativeTmdbApiKey);
            var hasImageOverride = !string.IsNullOrWhiteSpace(options.AlternativeTmdbImageUrl);
            if (!hasApiOverride && !hasImageOverride)
            {
                return Build(ItemStatus.Unavailable, "未启用", "N/A");
            }

            var statusText = string.Empty;
            var hasFailure = false;
            var hasSuccess = false;

            if (hasApiOverride)
            {
                var apiBaseUrl = NormalizeBaseUrl(options.AlternativeTmdbApiUrl, DefaultApiBaseUrl);
                var apiResult = await ProbeApiAsync(apiBaseUrl, options.AlternativeTmdbApiKey).ConfigureAwait(false);
                hasSuccess |= apiResult.Succeeded;
                hasFailure |= !apiResult.Succeeded;
                statusText += "API: " + apiResult.StatusText;
            }

            if (hasImageOverride)
            {
                var imageBaseUrl = NormalizeBaseUrl(options.AlternativeTmdbImageUrl, DefaultImageBaseUrl);
                var imageResult = await ProbeImageAsync(imageBaseUrl).ConfigureAwait(false);
                hasSuccess |= imageResult.Succeeded;
                hasFailure |= !imageResult.Succeeded;
                if (!string.IsNullOrEmpty(statusText))
                {
                    statusText += "\n";
                }

                statusText += "Image: " + imageResult.StatusText;
            }

            if (hasFailure && hasSuccess)
            {
                return Build(ItemStatus.Warning, "部分可用", statusText);
            }

            if (hasSuccess)
            {
                return Build(ItemStatus.Succeeded, "可用", statusText);
            }

            return Build(ItemStatus.Unavailable, "不可用", string.IsNullOrEmpty(statusText) ? "N/A" : statusText);
        }

        private static async Task<ProbeCoreResult> ProbeApiAsync(string baseUrl, string apiKey)
        {
            var url = CombineUrl(baseUrl, "/3/configuration");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                url += "?api_key=" + Uri.EscapeDataString(apiKey.Trim());
            }

            return await ProbeHttpAsync("GET", url, url, HttpStatusCode.Unauthorized).ConfigureAwait(false);
        }

        private static async Task<ProbeCoreResult> ProbeImageAsync(string baseUrl)
        {
            var url = CombineUrl(baseUrl, ImageProbePath);
            var result = await ProbeHttpAsync("HEAD", url, url).ConfigureAwait(false);
            if (!result.Succeeded && result.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                result = await ProbeHttpAsync("GET", url, url).ConfigureAwait(false);
            }

            return result;
        }

        private static async Task<ProbeCoreResult> ProbeHttpAsync(
            string method,
            string url,
            string displayUrl,
            HttpStatusCode? acceptedFailureStatus = null)
        {
            try
            {
                var httpClient = Plugin.SharedHttpClient;
                if (httpClient == null)
                {
                    return new ProbeCoreResult
                    {
                        Succeeded = false,
                        StatusText = displayUrl + " IHttpClient 不可用"
                    };
                }

                var requestOptions = new HttpRequestOptions
                {
                    Url = url,
                    TimeoutMs = (int)DefaultTimeout.TotalMilliseconds,
                    EnableHttpCompression = true,
                    EnableDefaultUserAgent = false,
                    UserAgent = "MediaInfoKeeper"
                };
                var stopwatch = Stopwatch.StartNew();
                using var response = await httpClient.SendAsync(requestOptions, method).ConfigureAwait(false);
                stopwatch.Stop();

                var statusCode = (HttpStatusCode)response.StatusCode;
                if (((int)response.StatusCode >= 200 && (int)response.StatusCode < 300) ||
                    statusCode == acceptedFailureStatus)
                {
                    return new ProbeCoreResult
                    {
                        Succeeded = true,
                        StatusCode = statusCode,
                        StatusText = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} {1} ({2} ms)",
                            displayUrl,
                            statusCode == acceptedFailureStatus ? "连通" : "可用",
                            stopwatch.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture))
                    };
                }

                return new ProbeCoreResult
                {
                    Succeeded = false,
                    StatusCode = statusCode,
                    StatusText = string.Format(CultureInfo.InvariantCulture, "{0} HTTP {1}", displayUrl, (int)response.StatusCode)
                };
            }
            catch (Exception ex)
            {
                return new ProbeCoreResult
                {
                    Succeeded = false,
                    StatusText = displayUrl + " " + ex.GetType().Name
                };
            }
        }

        private static string NormalizeBaseUrl(string raw, string fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            var value = raw.Trim();
            if (!value.Contains("://"))
            {
                value = "https://" + value;
            }

            return value.TrimEnd('/');
        }

        private static string CombineUrl(string baseUrl, string path)
        {
            return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
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

        private sealed class ProbeCoreResult
        {
            public bool Succeeded { get; set; }

            public HttpStatusCode StatusCode { get; set; }

            public string StatusText { get; set; }
        }
    }
}
