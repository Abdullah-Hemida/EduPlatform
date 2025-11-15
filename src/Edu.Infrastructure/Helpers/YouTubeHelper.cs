using System;
using Microsoft.AspNetCore.WebUtilities;

namespace Edu.Infrastructure.Helpers
{
    public static class YouTubeHelper
    {
        // Returns null if no id found
        public static string? ExtractYouTubeId(string urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId)) return null;

            urlOrId = urlOrId.Trim();

            // If it looks like a direct id (11 chars), return it
            if (urlOrId.Length == 11 && !urlOrId.Contains("://") && !urlOrId.Contains("/")) return urlOrId;

            // Try to parse as URI
            if (Uri.TryCreate(urlOrId, UriKind.Absolute, out var uri))
            {
                // youtu.be short links
                if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
                {
                    var id = uri.AbsolutePath.Trim('/');
                    return string.IsNullOrEmpty(id) ? null : id;
                }

                // youtube.com/watch?v=VIDEOID
                var q = QueryHelpers.ParseQuery(uri.Query);
                if (q.TryGetValue("v", out var v) && !string.IsNullOrEmpty(v))
                    return v.ToString();

                // /embed/VIDEOID or /v/VIDEOID
                var segments = uri.Segments;
                for (int i = 0; i < segments.Length; i++)
                {
                    var seg = segments[i].Trim('/');
                    if (string.Equals(seg, "embed", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
                        return segments[i + 1].Trim('/');
                    if (string.Equals(seg, "v", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
                        return segments[i + 1].Trim('/');
                }

                var last = uri.AbsolutePath.Trim('/');
                if (!string.IsNullOrEmpty(last)) return last;
            }
            else
            {
                // maybe the user entered only the id or partial url; return if plausible
                if (urlOrId.Length <= 50) return urlOrId;
            }

            return null;
        }
    }
}

