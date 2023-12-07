Example video:

https://github.com/steven4547466/YoutubeBoombox/assets/23726534/4b8d73c1-73ae-4d34-9afe-c8883575e3eb

I am sorry about the compression.

How to use (make sure you're on 1.0.3+, earlier versions of the mod had issues):
1. Obtain boombox
2. Left click while holding boombox
3. Paste URL
4. Click play
5. Wait a few seconds for everyone to download
6. Profit.

Features:
- Play music from youtube and sync to all clients as long as everyone has the mod
- Client side boombox volume control
  - Hold a boombox and type `/bbv number` to change the volume of that boombox on your client
    - Example: `/bbv 50` is half as loud
- Supports basic `youtube.com/watch` links and shortened `youtu.be` links
- Retains downloads until you restart the game, meaning playing the same songs in the same session only needs to download once, making repeats faster

TODO:
- Add a config option to keep the boombox on while in your inventory.
- Better UI.
- Allow the mod to be used in lobbies where not everyone has the mod by only waiting on the number of people with the mod.
  - Currently if you use the mod in a lobby and not everyone has it, it will wait forever.
- Add a max duration config option that you'll download.
  - To prevent someone from trying to download a 10 hour song to your computer.
  - Will most likely default it to 10 minutes.
- Add a max downloaded cache so it'll delete old downloads.
- Playlist support.