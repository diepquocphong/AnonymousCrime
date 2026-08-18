# Metro Police Station Mobile

Unity-ready environment asset generated procedurally with Three.js and imported as native Unity assets.

- Validated with Unity 6000.3.21f1 and URP 17.3.0.
- Main prefab: `Assets/PoliceStationMobile/Model/PoliceStation_Mobile.prefab`.
- Three static render batches and three shared opaque materials.
- Vertex colors provide all surface color variation; no runtime textures are required.
- Custom lightweight URP PBR shader is included in this folder.
- Box colliders are attached to the four `COL_*` hint nodes.
- One Unity unit equals one metre; Y-up; the building front faces local `-Z`.

For mobile builds, keep the renderer objects static and use baked or mixed scene lighting. The included preview image is documentation only and can be removed after import.

