This changelog only has changes from 1.2.0 onward.

# Version 1.3.1
- [Fix] Fixed an issue where only the host was able to start downloading songs
- [Fix] Fixed an issue where songs that "have drm" set to "maybe" in youtube API would cause song info downloading to break
- [Fix] Fixed an issue where songs with odd characters could not be downloaded (#9)
- [Fix] Fixed a potential issue with links that have additional information attached in query parameters

# Version 1.3.0
- [Rewrite] Completely rewrote the networking using unity NGO rpcs. Thanks to [UnityNetcodeWeaver](https://github.com/EvaisaDev/UnityNetcodeWeaver)
  - It should now be much more stable
- [Fix] Fixed an issue where the ui would pop up for everyone when the boombox died and would fail to stop the audio

# Version 1.2.0
- [Addition] Support for playlists
- [Addition] The boombox volume command will now affect the closest boombox within 15m if you are not holding one
- [Addition] Added `MaxCachedDownloads` config option
  - Defaults to `10`, when more songs are downloaded the oldest one is deleted
- [Addition] Added `DeleteDownloadsOnRestart` config option
  - Defaults to `true`
- [Addition] Added `MaxSongDuration` config option
  - Defaults to `600`. Value is in seconds
  - Songs longer than this value won't be downloaded and you will hear whatever was last played through the boombox instead, if anything