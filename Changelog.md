This changelog only has changes from 1.2.0 onward.

# Version 1.4.0
- [Addition] Added a configurable keybind to open the menu instead of left click. Defaults to "B"
  - While holding a boombox, you will see a GUI in the bottom right that says which button to press
- [Change] Default boombox functionality is restored. Left click will use it like normal instead
  - This is for compatibility with mods that add soundtracks directly
- [Change] As long as the host has the mod, no one requires the mod for it to play
  - Anyone that doesn't have it won't hear anything, though. But you will be able to play custom songs without everyone having the mod
- [Change] While the GUI is open, you won't be able to look around
  - Still looking for a way to disable movement and crouching
- [Fix] Fixed an issue in `youtu.be` links that have share information attached. The previous fix was only for `youtube.com/watch` links

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