Example video:

https://github.com/steven4547466/YoutubeBoombox/assets/23726534/4b8d73c1-73ae-4d34-9afe-c8883575e3eb

I am sorry about the compression.

How to use:
1. Obtain boombox
2. Use your configured hotkey to open the boombox menu (B by default)
3. Paste URL
4. Click play
5. Wait a few seconds for everyone to download
6. Profit.

Features:
- Play music from youtube and sync to all clients as long as everyone has the mod
- Client side boombox volume control
  - Type `/bbv number` to change the volume of the closest boombox on your client within 15m
    - If you are holding a boombox, the command will always target that one
    - Example: `/bbv 50` is half as loud
- Supports basic `youtube.com/watch` links and shortened `youtu.be` links
- Supports playlist links like `youtube.com/playlist?list=LIST_ID` ensure there's no `v=` in there, otherwise it'll play just the single video
- Retains a configurable amount of downloads until you restart the game (which is also configurable), meaning playing the same songs in the same session only needs to download once, making repeats faster

TODO:
- Add a config option to keep the boombox on while in your inventory.
- Better UI.
- Allow the mod to be used in lobbies where not everyone has the mod by only waiting on the number of people with the mod.
  - Currently if you use the mod in a lobby and not everyone has it, it will wait forever.