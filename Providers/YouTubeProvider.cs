using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeBoombox.Providers
{
    public class YouTubeProvider : Provider
    {
        public override string[] Hosts => new string[] { "youtube.com", "www.youtube.com" };

        public override ParsedUri ParseUri(Uri uri)
        {
            string id = string.Empty;
            UriType uriType = UriType.Video;

            NameValueCollection collection = HttpUtility.ParseQueryString(uri.Query);
            id = collection.Get("v");

            if (id == null)
            {
                id = collection.Get("list");
                uriType = UriType.Playlist;
            }

            if (id == null || id == string.Empty) return null;

            return new ParsedUri(uri, id, uri.Host + uri.PathAndQuery, uriType);
        }
    }

    public class YouTuBeProvider : Provider
    {
        public override string[] Hosts => new string[] { "youtu.be", "www.youtu.be" };

        public override ParsedUri ParseUri(Uri uri)
        {
            return new ParsedUri(uri, uri.AbsolutePath.Substring(1), uri.Host + uri.AbsolutePath, UriType.Video);
        }
    }
}
