Source art lives here uncommitted; `.gitignore` keeps the GLBs out. Game-ready assets go
under `Assets/Art/`.

## Characters

The current roster arrives as Mixamo-rigged FBX and needs no conversion. Install one with

    python3 Tools/prepare_rigged_characters.py MiaByBy3D=~/Downloads/Miabyby-pose.fbx

which writes `Assets/Art/Characters/<CharacterId>/` with the model and its base colour map.
A new id also has to be listed in `MiaCourtAssets.CharacterIds` before running
`Mia Court / Configure URP`.

`Tools/prepare_characters.py` is the legacy Blender path for the ten static GLB characters
retired on 2026-09-22. Nothing in the game uses it now.

## Props

The scene props arrive here as Tripo GLBs and are converted by `Tools/prepare_props.py`
into the OBJ and PBR maps under `Assets/Art/Environment/Props/`. See that script for the
virtual environment it needs.
