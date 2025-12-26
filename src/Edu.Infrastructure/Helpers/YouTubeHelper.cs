using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Text.RegularExpressions;

namespace Edu.Infrastructure.Helpers
{
    public static class YouTubeHelper
    {
        // Returns null if no id found
        public static string? ExtractYouTubeId(string urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId)) return null;
            urlOrId = urlOrId.Trim();

            // Direct ID case (11 chars, allowed chars)
            if (urlOrId.Length == 11 && Regex.IsMatch(urlOrId, @"^[A-Za-z0-9_-]{11}$"))
                return urlOrId;

            // Try URI parse
            if (Uri.TryCreate(urlOrId, UriKind.Absolute, out var uri))
            {
                // youtu.be short links -> path
                if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
                {
                    var id = uri.AbsolutePath.Trim('/');
                    if (!string.IsNullOrEmpty(id) && id.Length >= 11)
                        return id.Split('?', '&')[0];
                }

                // v= query param
                var q = QueryHelpers.ParseQuery(uri.Query);
                if (q.TryGetValue("v", out var v) && !string.IsNullOrEmpty(v))
                {
                    var maybe = v.ToString();
                    if (maybe.Length >= 11) return maybe.Split('?', '&')[0];
                    return maybe;
                }

                // /embed/VIDEOID or /v/VIDEOID etc. use regex on path
                var path = uri.AbsolutePath;
                var rx = new Regex(@"(?:/embed/|/v/|/watch/|/videos/|/shorts/)([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase);
                var m = rx.Match(path);
                if (m.Success && m.Groups.Count > 1)
                    return m.Groups[1].Value;

                // as last resort, find an 11-char token anywhere in the path or query
                var fallbackRx = new Regex(@"[A-Za-z0-9_-]{11}", RegexOptions.Compiled);
                m = fallbackRx.Match(uri.PathAndQuery);
                if (m.Success) return m.Value;

                // nothing
                return null;
            }

            // not an absolute URI: maybe they pasted "watch?v=..." or the id with extra chars
            // try to pull an 11-char token from the provided string
            var fallback = Regex.Match(urlOrId, @"[A-Za-z0-9_-]{11}");
            return fallback.Success ? fallback.Value : null;
        }
    }
}

