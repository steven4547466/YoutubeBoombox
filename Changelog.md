This changelog only has changes from 1.2.0 onward.

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