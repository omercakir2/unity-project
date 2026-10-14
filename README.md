# Unity Third Person Controller

Third person character controller with camera-relative movement, sprint, and mouse look using new Input System.

## Current Setup
- `PlayerController.cs` + CharacterController on Player
- `ThirdPersonCamera.cs` on Main Camera with mouse look
- `PlayerInput` component wired to Move, Sprint, and Look actions
- CameraTarget empty child used for camera following

## Team Workflow (Important)

**Scene changes are saved in `Assets/Scenes/SampleScene.unity`**

- You **must** press `Ctrl+S` (or File → Save) after every change in the Scene view or Inspector. If you forget, changes are lost when you close Unity.
- All `.unity`, `.prefab`, and `.meta` files are committed to Git.
- **Never** edit the same scene at the same time with teammates — it creates massive merge conflicts.

### Best Practices for Team Development

1. **Use Prefabs aggressively**
   - Turn Player into a Prefab.
   - Turn camera setup into a "CameraRig" Prefab.
   - Keep main scene as minimal as possible (just spawns prefabs).

2. **Save early, commit often**
   - Save scene after every meaningful change.
   - Commit with clear messages describing what you changed in the scene/prefabs.

3. **Configure merge tool**
   - Unity → Edit → Preferences → Version Control → Use "UnityYAMLMerge" for smart scene merging.
   - Or use Unity Version Control (Plastic SCM).

4. **ProjectSettings**
   - Most files in `ProjectSettings/` should be committed (input settings, tags, layers, graphics, etc.).

5. **Scripts**
   - All logic stays in `Assets/Scripts/`. These are easy to merge.

## How to Run
1. Open project in Unity 2022.3+
2. Open `SampleScene`
3. Play (WASD + Shift to sprint, Mouse to look)

Made with ❤️ using Grok.

Last updated: 2026-10-14
