using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeBoombox.Providers
{
    public enum UriType
    {
        Video,
        Playlist
    }

    public class ParsedUri
    {
        public Uri Uri { get; }

        public string Id { get; }

        public string DownloadUrl { get; }

        public UriType UriType { get; }

        public ParsedUri(Uri uri, string id, string downloadUrl, UriType uriType)
        {
            Uri = uri;
            Id = id;
            DownloadUrl = downloadUrl;
            UriType = uriType;
        }
    }

    public abstract class Provider
    {
        public abstract string[] Hosts { get; }

        public abstract ParsedUri ParseUri(Uri uri);
    }
}
