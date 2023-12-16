// Soundcloud's api requires authentication, meaning we can't grab track duration data. This would mean all soundcloud
// tracks would bypass any duration check, which I'm not going to allow at this time.

//using System;
//using System.Collections.Generic;
//using System.Collections.Specialized;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace YoutubeBoombox.Providers
//{
//    public class SoundcloudProvider : Provider
//    {
//        public override string[] Hosts => new string[]{ "soundcloud.com", "www.soundcloud.com" };

//        public override ParsedUri ParseUri(Uri uri)
//        {
//            return new ParsedUri(uri, uri.AbsolutePath.Substring(1).Replace("/", "-yb-"), uri.Host + uri.AbsolutePath, uri.AbsolutePath.Contains("sets") ? UriType.Playlist : UriType.Video);
//        }
//    }
//}
