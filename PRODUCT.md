# 喵喵街頭籃球

<!-- impeccable:product-schema 1 -->

## Platform

Unity desktop game, initially Windows.

## Confirmed brief

Create a playable Unity 3D basketball game. The roster is the eight supplied models: MiaByBy3D is 喵白白; MiaBuBu3D is 喵布布; GuguGaga3D is 咕咕嘎嘎; YaMei3D is 牙妹; YuMei3D is 魚妹; FengBro3D is 鋒兄; Tu3D is 塗董; DpskMusume3D is 深索娘. Use the three supplied Taipei sunset court images in the explicit order left, center, right. Preserve these names and this mapping.

On 2026-09-22 the user set the match rules and the feel of the action: a match runs 3 minutes 30 seconds and is won outright by the first side to 21 points, otherwise by whoever leads when the clock expires. Shot power must be committed inside 3 seconds. The characters must visibly dribble, shoot, steal and block rather than slide about in a fixed pose.

Also on 2026-09-22 the user set the shape of a session and how hard it is. A game is either a single match or a knockout cup: the eight characters are drawn into an eight-team bracket - quarter-finals, semi-finals, then a final and a third-place match - in which the player plays three matches at most. Difficulty is a three-way choice, and the user gave it as success rates. Shooting: easy is 93% from two and 33% from three, normal 83% and 23%, hard 73% and 13%. Defence: easy is a 53% steal and a 73% block, normal 33% and 53%, hard 13% and 33%.

The shooting percentages are implemented as the odds for a clean, unguarded release; a mistimed release or a contested one scales them down, so the shot meter still decides something. The defensive percentages are rolled only once the swipe or the contest has actually reached the ball carrier or the shot, so reaching is still a matter of position and timing and the percentage decides what the hand does when it gets there. A failed attempt spends the swipe. The user did not say whether the same setting should make the computer opponent stronger, and it does: its own shooting odds, reaction time, speed and willingness to swipe all move with the setting. The computer's numbers are an implementation choice, not something the user stated.

The user replaced the original ten static characters with these eight rigged ones on 2026-09-22. 小塗, 鋒市 and 鋒總 were retired at the same time. 深索娘 is a new character; the Chinese name is an assumption drawn from the supplied file name Dpskmusume, not something the user stated.

## Assets

The characters are supplied as Mixamo-rigged FBX and used as delivered, without decimation. Each carries a bound skin over a mixamorig skeleton and a single baked pose instead of an animation clip. `Tools/prepare_rigged_characters.py` installs one: it saves the 2048x2048 colour map the exporter embedded in the FBX as `<Character>_BaseColor.png` and drops the embedded copy, which Unity never reads because materials are not imported. Geometry, skeleton and bind pose are untouched, and the script can prove it by rewriting a source unedited and comparing byte for byte. Unity imports the models as Humanoid so an avatar exists for future retargeted clips. The Animator the importer leaves on each instance is dropped at spawn and every gesture is posed procedurally: a stride, a torso counter-rotation, and two-bone IK solved in world space onto the mixamorig arms, so the dribble, gather, shot follow-through, steal rake and block all read on any of the eight skeletons without a single clip.

The scene props - basketball, chain-link fence panel, courtside bench, floodlight pole, trash bin and a parked scooter - are generated from text in Tripo and converted by `Tools/prepare_props.py`: each one million-triangle GLB is decimated to a few thousand triangles, scaled to its real-world size and written as an OBJ with 2048x2048 base colour and normal maps, imported at 1024. The match hoop stays procedural because the rim it must match is deliberately oversized for a forgiving arc.

## Current implementation assumptions

Keyboard and mouse on Windows; one human versus the computer; two- and three-point shots; a 30-second overtime when the clock expires level. In a single match the player and opponent are chosen independently from the roster; in the cup only the player is chosen and the other seven are drawn. These are implementation defaults, not claimed user preferences. The match length, the 21-point target and the 3-second shot bar are not defaults - they are in the confirmed brief above.

## Visual commitments

The supplied sunset photographs define the setting. A real 3D court, hoops, ball and the supplied characters lead the screen. Traditional Chinese interface. Left, center and right camera presets use the corresponding original photograph as the distant backdrop.
