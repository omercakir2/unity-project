# 5-minute demo — build order

Working plan for the short game. The order below is the order of the work. Finish a phase before starting the next one.

| | |
|---|---|
| Project | `My project` |
| Unity | 6000.6.5f1 |
| Render pipeline | URP 17.6 |
| Written | 2026-10-29 |
| Player time | One sitting, under 5 minutes |
| First finishable build | End of Phase 4 |

This file is the copy we both work from. `docs/5-minute-demo-plan.pdf` is the same text for reading away from the editor.

---

## How to use this

Each phase starts with the to-do, in the order to do it, then the detail you need to implement it. A phase is done only when its **Done when** line is true in Play mode.

Save the scene after every block of editor work (`Cmd+S`). Scene edits that are not saved are gone when Unity closes.

Phases 1–4 are the game. Phases 5–9 are the look that was asked for: cinematic cameras, animation, fire, blood, snow, and a camera pass. If a day runs long, protect Phases 1–4. The cut order is at the end.

---

## Master to-do

Do these in this order. Do not pull a later phase forward because it is more interesting.

1. **Phase 1 — Mark the path.** Two small scenes, spawn points, one zombie, one door, three sticks, one fire pit, fences. Time the walk.
2. **Phase 2 — His half plays.** Wake prompt, walk, shoot, door. The door loads her scene.
3. **Phase 3 — Her half plays.** Second character, cold clearing, pick up sticks, light a fire.
4. **Phase 4 — Front and back of the game.** Start menu, objective line, end card. A stranger can finish the demo.
5. **Phase 5 — Cinematic cameras.** Timeline plus the Cinemachine cameras that already exist. Default shot list, easy to reorder.
6. **Phase 6 — Animation the beats need.** Tired pose we already have, plus a pickup clip. Get-up from the ground only if the camera version is not enough.
7. **Phase 7 — Fire and blood.** Particle System fire, Particle System blood, one URP decal under the body.
8. **Phase 8 — Snow.** White caps on props. A ground blanket that sinks where the feet step. Falling snow in her scene.
9. **Phase 9 — Camera, motion, clock.** Tune the built-in third-person follow. Time a full run. Freeze.

### Days

| Day | Phases | You can stop when |
|---|---|---|
| 1 | 1 and 2 | He shoots the zombie and the door loads her scene |
| 2 | 3 and 4 | Menu → him → her → fire → end card, in one build |
| 3 | 5 and 6 | Cameras move on their own, pickup reads clearly |
| 4 | 7 and 8 | Fire, blood, and foot-pressed snow are in |
| 5 | 9 | A full run is under 5 minutes and written down |

---

## What the player goes through

Story that is still open: their names, how they were separated, why this world is wrong, whether they find each other again. None of that is required to build the demo. On-screen lines live in one table at the end so a wording change is an edit in one place.

What is locked:

- They were close. Husband and wife is the working picture.
- They are in another version of the world. It is dark. His side is raining. Her side is cold and snowing. Zombies exist here. They do not know why.
- He wakes in an industrial yard with no memory.
- He walks for about 10–20 seconds, meets one zombie, and shoots it.
- He opens a door. We leave his scene.
- She is alone in a forest. She has no memory. It is cold.
- She picks sticks up off the ground, lights a fire, and the demo ends.

### Clock

Walk speed on the **demo scene instances** is `2.4` m/s (the shared prefab stays at `5`). A 15-second walk is about 36 meters. Sprint on those instances is set to the same `2.4`, so holding Shift does not shrink the walk.

| Beat | Target | Ceiling |
|---|---|---|
| Menu | 5 s | 15 s |
| His opening camera | 12 s | 18 s |
| Walk until the zombie is in view | 12–18 s | 25 s |
| Shoot | 5 s | 15 s |
| Door and camera through it | 8 s | 12 s |
| Her opening camera | 8 s | 12 s |
| Find 3 sticks and light the fire | 50–70 s | 90 s |
| Fire camera and end card | 12 s | 18 s |
| **Total** | **about 3 minutes** | **5:00** |

The spare minute is for a missed shot or a stick that is hard to see. It is not spare level to fill.

### His yard

About 40 m by 24 m, closed in by fences or invisible walls.

```
[ hangar / spawn ] --about 14 m--> [ open yard ]
                                        |
                                        | about 12 m, around a container
                                        v
                                   [ one zombie ]
                                        |
                                        | about 10 m
                                        v
                                   [ the door ]
```

He should not see the zombie from the spawn. The container, a wall, or the hangar corner hides it. He should not see the door until the zombie is down, or the door is visible but does not open yet.

### Her clearing

About 24 m across. Trees ring the outside. She cannot walk out into the 250 m forest.

```
                 (stick)
    (spawn)                  (stick)

                 (fire pit)

                 (stick)
```

The pit is near the middle. Each stick is 8–12 m from it, on the ground, in sight if she turns once.

---

## What is already in the project

Use these. The phases say when.

| Need | Already here |
|---|---|
| Him | `Assets/Game/Prefabs/Characters/Player.prefab`. Male mesh, Humanoid, `MaleAnimator.controller` |
| Her mesh | `Assets/Game/Models/Characters/Ch44_nonPBR.fbx`. Imported as Humanoid (`animationType` 3). Same animator can drive her |
| Third person | Cinemachine 6.6. `CinemachineThirdPersonFollow` on `Third Person Aim Camera`, obstacle avoidance already on. Look input is `CinemachineThirdPersonLook` on the `Third Person Aim` pivot. Menu: **Tools → Setup Cinemachine Camera** |
| Old camera script | `ThirdPersonCamera.cs` is an empty leftover. Leave it on the prefab so old scenes do not lose the reference. Do not put new code in it |
| Pistol | Hotbar slot 3, `PistolShot` hitscan, slide kick, tracer, gunshot audio. `ZombieController.Kill()` already plays the death clip |
| Zombie | `Assets/Game/Prefabs/Characters/Zombie.prefab`. Crawl, stand, scream, run, then chase. Death clip is in the controller |
| Tired body | Low energy blends in `Remy@Tired-Idle` through `PlayerVitals` and `PlayerController` |
| Rain | `EnvironmentRain` starts in every scene on its own. Settings: `Assets/Game/Resources/AmbienceSettings.asset` |
| Night grade | `IndustrialYard_Night` already has a Global Volume profile |
| Forest art | `Demo_01` / `Demo_01_Night`, Pure Poly trees, `New Forest Terrain` |
| Yard art | `IndustrialYard_Night` plus `Assets/Imported/Industrial/` (hangars, containers, fences, smoke textures) |
| Interact key | **E**, action `Interact`. Today `PlayerController.OnInteract` only writes a log line |
| Shoot key | Left mouse, action `Attack` |
| UI stack | uGUI and TextMeshPro. Health bars are built by `VitalsHud` on the player canvas. Input is **Input System only** (`activeInputHandler: 1`) |
| Timeline | Package `com.unity.timeline` 6.6 is installed |
| Shader Graph | `com.unity.shadergraph` 17.6 comes with URP |
| Music and breath | `MainThemeMusic` and `PlayerBreath` already start on their own |

`docs/architecture.html` describes an older camera (`ThirdPersonCamera` doing the orbit). The live camera is the Cinemachine rig. Follow this plan for camera work.

---

## Rules for every phase

**Packages stay as they are.** Cinemachine, Timeline, Input System, uGUI, TextMeshPro, AI Navigation, and URP are already installed. Do not add VFX Graph, a new controller asset, ProBuilder, or an interaction framework.

**Built-in pieces we will use**

- Cinemachine Brain, Cinemachine Camera, Third Person Follow, its Avoid Obstacles settings, and the Cinemachine Timeline track
- Timeline signals when a camera move must hand control back
- uGUI Canvas, Button, Image, CanvasGroup, and TextMeshPro
- Input System `PlayerInput` (Invoke Unity Events) and `InputSystemUIInputModule` on menus
- Particle System with shader `Universal Render Pipeline/Particles/Unlit`
- URP Decal Renderer Feature for the blood stain
- URP Volume (Color Adjustments, Vignette) and the Lighting window fog
- Humanoid retarget onto `MaleAnimator.controller`
- `SceneManager.LoadScene`

**Glue we are willing to write.** Short scripts that connect those pieces. No second camera system, no snow physics solver, no dialogue system, no inventory rewrite.

**Scene instances hold the demo tuning.** `walkSpeed`, `sprintSpeed`, and starting energy are overrides on the player sitting in the demo scene. `Assets/Game/Prefabs/Characters/Player.prefab` stays the gym character. SampleScene, Demo_01, and the night forest keep working.

**One gameplay scene loaded at a time.** His yard unloads when her clearing loads. That avoids two players and two Cinemachine brains.

---

## Phase 1 — Mark the path

**Done when:** In each scene you can walk the route under the clock targets, the zombie is hidden from the spawn, and invisible walls stop you leaving the area.

### To-do

1. Create the two scenes without touching the gym scenes.
2. Drop in his player, her stand-in, the zombie, the door, the sticks, and the pit as markers.
3. Fence both spaces.
4. Time his walk with a stopwatch. Move the zombie until the walk is 12–18 seconds at the demo speed.
5. Save both scenes.

### How

**Scenes**

- `Assets/Game/Scenes/Demo/Demo_Act1_Yard.unity`
- `Assets/Game/Scenes/Demo/Demo_Act2_Clearing.unity`

Act 1 starts as a duplicate of `Assets/Game/Scenes/IndustrialYard_Night.unity`. In the Project window, select that scene and press **Cmd+D**, then move the copy to `Assets/Game/Scenes/Demo/` and rename it. It already has night lighting and a volume. Walk it once.

- If a natural corner already takes about 15 seconds at speed `2.4`, put the beats on that corner.
- If the yard is tiny, extend it with containers and fence from `Assets/Imported/Industrial/`.
- If the yard is huge, do not delete the city. Box in one 40×24 block with the concrete fence prefabs and play only inside it.

Act 2 is a **new empty scene**, not a duplicate of the 250 m forest. A full duplicate is heavy and she would walk for minutes.

- **File → New Scene**, choose the same URP setup the night scenes use.
- Ground: a Unity plane scaled up, material `Assets/Game/Materials/GroundMaterial.mat`, or a small terrain. The playable dirt only needs to cover the clearing.
- Ring the clearing with a handful of Pure Poly tree prefabs dragged in from `Assets/Game/Models/Forest` or `Assets/Imported/PurePoly`. A ring, not a forest generator.
- Directional light: low angle, cool color, intensity under the yard’s night light so it feels like the same wrong weather.
- Add a **Global Volume**. Profile: Color Adjustments with temperature pulled toward blue, exposure a little under 0, a light Vignette. Turn on fog in **Window → Rendering → Lighting → Environment**: linear or exponential, blue-grey, thick enough that trees a few meters out go soft.
- Sky: `Assets/Game/Materials/Skybox_Night.mat` if it fits, or the industrial night skybox.

If a duplicated scene hides meshes that you can still select, the old occlusion data is stale. **Window → Rendering → Occlusion Culling → Clear**, then bake later if you want it. Correctness first.

**Markers.** Empty objects, named exactly:

| Name | Scene | What it marks |
|---|---|---|
| `Spawn_Him` | Act 1 | Where the player root stands |
| `ZombieEncounter` | Act 1 | Zombie root |
| `Door` | Act 1 | The physical door |
| `Spawn_Her` | Act 2 | Her root |
| `Stick_01` `Stick_02` `Stick_03` | Act 2 | Pickups |
| `FirePit` | Act 2 | Where the fire will be |

**Him, for the timing pass only.** Drag `Assets/Game/Prefabs/Characters/Player.prefab` into Act 1 at `Spawn_Him`. On that **instance** (not the prefab asset):

- `PlayerController.walkSpeed = 2.4`
- `PlayerController.sprintSpeed = 2.4`
- Tag the root `Player` if the prefab is not already tagged. The zombie finds `Player`.

**Zombie.** Drag `Assets/Game/Prefabs/Characters/Zombie.prefab` onto `ZombieEncounter`. Rotate it so the crawl faces the direction he will arrive from. The script already turns to face the player and stays put until the animator reaches `Running`, then chases at `6.2` m/s. One shot calls `Kill()`. No AI changes in this phase.

**Door visual.** A container door from `Assets/Imported/Industrial/Containers/` or a tall cube. It only has to read as a door. It does not open yet.

**Her stand-in.** The male player prefab is enough to time her space. Phase 3 swaps the mesh. Put three brown cylinders at the stick markers (scale about `0.08, 0.6, 0.08`, rotated so they lie on the ground). Put a circle of small rocks or boxes at `FirePit`.

**Fences.** Real fence meshes where they look right. Where they would look fake, empty objects with Box Colliders, named `Bounds`, layer Default. About 2 m tall. Leave no gap at the corners. She must not be able to reach the big forest because that forest is not in this scene.

**Camera for the timing pass.** Drag `Assets/Game/Prefabs/Cameras/Main Camera.prefab` and `Assets/Game/Prefabs/Cameras/Cinemachine Cameras.prefab` into each scene if the duplicate did not bring them. Press Play. WASD, mouse look. If the brain has no follow target, assign the player’s `Third Person Aim` pivot the same way the other night scenes do, or run **Tools → Setup Cinemachine Camera** and then re-check it did not rewrite a scene you did not mean to touch. That menu lists specific scene paths. Prefer assigning the follow target by hand in the two new scenes.

**Clock.** Start Play at `Spawn_Him`, walk (do not sprint) the intended route, start the watch when the camera lets you move, stop when the zombie is clearly in view. Move `ZombieEncounter` until that is 12–18 seconds. Then walk to the door and confirm it is only a few more seconds.

### Check

- Gym scenes were not moved or retuned.
- Act 1 walk to the zombie is 12–18 s at speed 2.4.
- Act 2 sticks and pit are inside the fence.
- Both scenes saved.

---

## Phase 2 — His half plays

**Done when:** From `Spawn_Him`, with no editor help, he walks, shoots the zombie once, hears it die, and E on the door loads Act 2.

### To-do

1. Add `IsDead` and a killed event on the zombie.
2. Add a small interact lookup and hook it to the existing E key.
3. Door opens only after this zombie is dead, then loads Act 2.
4. Start him on the pistol, tired, and slow.
5. Put a one-line subtitle in the yard.
6. Play the whole act.

### How

**Zombie, one addition.** In `ZombieController`, `dead` is private. Add:

```csharp
public bool IsDead => dead;
public UnityEvent Killed;
```

At the end of `Kill()`, call `Killed?.Invoke();`. Nothing else in that class changes. Chase, death clip, and the player-touch death stay.

**Interact.** New files under `Assets/Game/Scripts/Demo/`.

`IInteractable.cs`

```csharp
public interface IInteractable
{
    string Prompt { get; }
    void Interact(PlayerController player);
}
```

`PlayerUse.cs` on the player:

- Each `Update`, `Physics.OverlapSphere` at `camera.position + camera.forward * 1.4f`, radius `0.9`.
- Keep colliders that have `IInteractable` on that object or a parent.
- Choose the one whose direction from the camera has the highest dot with `camera.forward`. Ignore dots below `0.35` so things behind him do not light up.
- Store it as the current target. A world-space or screen TextMeshPro shows `Prompt` when a target exists. Phase 4 replaces this with the shared banner. For this phase a single TMP text under the existing canvas is enough.
- Public method `TryUse()` calls `Interact` on that target.

`PlayerController.OnInteract` already runs when E is pressed (`context.performed`). After the log line, call `GetComponent<PlayerUse>()?.TryUse();`. That is the only change in `PlayerController`.

**Door.** `DoorGate.cs` on `Door`, implements `IInteractable`.

- Serialized `ZombieController encounter`. Drag the yard zombie in. Do not use `Find`.
- Serialized `Transform hinge`. The swinging mesh.
- A trigger collider on a child `UseVolume` (radius or box about 2 m). The door mesh keeps a solid collider so he cannot walk through it while it is shut.
- `Prompt` returns `The door is shut.` while `encounter` is alive, and `Open` when `encounter.IsDead` or the reference is empty.
- `Interact`: if the zombie is still alive, return. If the door already moved, return.
- Swing: coroutine, `hinge.localRotation` slerps 90 degrees on Y over `0.6` s. Disable the solid door collider at the start of the swing so the mesh does not shove him.
- At the end of the swing, `SceneManager.LoadScene("Demo_Act2_Clearing");`

Add both demo scenes in **File → Build Settings**. Order does not matter until Phase 4. They must be in the list or `LoadScene` by name fails in a build. In the editor it can still load by name if the scene is saved.

**Loadout on the Act 1 instance only.** `DemoLoadout.cs`:

```csharp
public enum DemoHands { LeaveInventory, Pistol, Empty }

[SerializeField] private DemoHands hands = DemoHands.LeaveInventory;
```

`Start` (so it runs after `PlayerInventory.Awake`, which fills torch / searchlight / pistol and selects slot 0):

- `Pistol`: `inventory.SelectSlot(2)`. `PistolShot` only fires when `ActiveItemId` is `pistol` and the pose weight is above `0.55`. The pose eases in over about `0.32` s, so the gun is ready before he reaches the zombie.
- `Empty`: set every `InventorySlotState.occupied` that `GetSlot` returns to false, then the inventory must refresh. Add `public void NotifyChanged()` on `PlayerInventory` that runs the existing `Changed` event, and call it after clearing. `HeldItemVisual` already listens and will hide the props.

Act 1 instance: `DemoHands.Pistol`.

On that same instance’s **Player Input** component (Invoke Unity Events), clear the persistent calls for Hotbar1, Hotbar2, Hotbar3, Hotbar4, and HotbarScroll. The shared input asset stays. He cannot swap away from the pistol and get stuck holding a torch for the only fight in the demo.

**Tired.** On the Act 1 instance, set `PlayerVitals` energy to `0`, `energyRegenIdlePerSecond` to `0`, `energyRegenWalkPerSecond` to `0`. Under 20% energy the existing blend moves him into the tired idle. Jump is already blocked while energy is low (`CanJump`). Confirm Space does nothing in Play. If it still jumps, clear the Jump event on this instance too.

**Subtitle.** A TMP text, top third of the screen, 4–5 seconds, then the object disables itself. Words from the copy table: his wake line. A 15-line `LineBanner` with a serialized string and duration is enough. Phase 4’s objective banner can replace it. Do not build a subtitle system.

**Death.** If the zombie touches him, `PlayerVitals` already dies and `VitalsHud` shows Restart. Leave that. One missed shot is allowed inside the 5 minutes. Do not add combat.

**Background zombies.** Optional, and only after the timed route works. Two extra prefab instances behind a fence, with `ZombieController` **disabled** and the Animator playing `Crawl` on the controller’s default state. They sell “there are more” without a second fight. If disabling the component also stops the Animator, leave the component and set `runSpeed` to `0` is **not** enough, because `Kill` and chase still exist. Prefer the Animator-only copies. The door must reference the one real zombie, so background bodies cannot block it.

### Check

- E does nothing useful until he looks at the door.
- The door does nothing while the zombie is alive.
- After the death animation starts, E swings the door and Act 2 loads.
- SampleScene player still starts on the torch with normal walk speed `5`.
- Console shows no missing-script warnings.

---

## Phase 3 — Her half plays

**Done when:** Act 2 loads with her mesh, she picks up three sticks with E, E on the pit lights a fire, and the end card appears.

### To-do

1. Duplicate the player prefab and put Ch44 on the copy.
2. Empty her hands. Hide her hotbar and the health bars.
3. Sticks go into a counter, not the hotbar.
4. The pit accepts E only at 3 sticks and turns a fire object on.
5. Show the end card from the pit’s event.
6. Turn rain off in this scene.

### How

**Prefab.** Duplicate `Assets/Game/Prefabs/Characters/Player.prefab` to `Assets/Game/Prefabs/Demo/Player_Her.prefab`.

Do **not** run **Tools → Player → Swap Model**. That menu writes the shared `Player.prefab` and would replace him in every scene.

Open `Player_Her` in prefab mode:

- Under `ModelSlot`, delete the male mesh child.
- Drag `Assets/Game/Models/Characters/Ch44_nonPBR.fbx` under `ModelSlot`. Local position and rotation `0`, scale `1`.
- On that child, assign `Assets/Game/Animations/Controllers/MaleAnimator.controller`, `Apply Root Motion` off. `PlayerAvatar.Bind` does this again at runtime if `animatorController` is set on the root.
- On the root `PlayerAvatar`, set `modelPrefab` to the Ch44 FBX.
- Press Play in Act 2 with this prefab at `Spawn_Her`. If the feet float or sink, move `ModelSlot` on Y until the soles meet the bottom of the capsule. Do not scale the capsule and do not scale the FBX root away from `1` unless the mesh is huge. Ch44 and the male share a Humanoid rig, so walk, strafe, and tired idle retarget with no new controller.

If Ch44 reads as the wrong person once you see her, swap the FBX under `ModelSlot` later. The rest of Act 2 does not care which humanoid it is. The zombie mesh (`Zombiegirl`) stays the zombie.

**Instance overrides**, same idea as him:

- `walkSpeed` and `sprintSpeed` `2.4`
- energy `0`, both energy regen rates `0`, so she idles tired in the cold
- `DemoLoadout` hands = `Empty`
- Disable the child `Vitals` under `InventoryCanvas` (the bars). Leave `DeathPanel` in case we add a hazard later.
- Disable the hotbar object (the widget `InventoryHotbarView` sits on). Clear Hotbar and Attack events on her `PlayerInput` so keys 1–4 and the mouse do not equip a gun she should not have.
- Keep Move, Look, Interact.

**Sticks.** `StickBag.cs` on her player. `public int Count { get; private set; }` and `public void AddOne()`. There are 3 sticks.

`StickPickup.cs` on each stick, implements `IInteractable`:

- Prompt: `Take the wood`
- A trigger collider. No solid collider, so she walks over them.
- `Interact`: if this stick was already taken, return. Disable renderers and colliders. `GetComponent<StickBag>()` on the player `AddOne()`.
- A short TMP on the banner: `Wood 1/3` using `Count`.

**Pickup pose.** Phase 6 adds a clip. This phase she stays in idle and the stick vanishes. That is acceptable for the Day 2 finish.

**Fire object.** Empty `FirePit` with:

- A trigger `UseVolume`
- A child `FireVisual`, **inactive**. For this phase the visual can be a Point Light (intensity `3`, color `#ff7a3c`, range `8`) plus a small orange cone. Phase 7 replaces the cone with the real particle prefab without changing `FirePit`.
- `FirePit.cs` implements `IInteractable`
  - Prompt if `StickBag.Count < 3`: `Need more wood`
  - Prompt at 3: `Light the fire`
  - `Interact` at 3: enable `FireVisual`, set a `lit` flag, invoke `public UnityEvent Lit`
- Wire `Lit` in the inspector to a `DemoEnd` object.

**End card, first version.** uGUI panel in Act 2, inactive, full screen, sort order above gameplay. Title line from the copy table. Button **Back** is optional until the menu exists. `DemoEnd.Show()` activates the panel and sets `PlayerInput` deactivated so she stops walking. Phase 5 inserts the fire camera **before** `Show`, by playing a Timeline from `Lit` instead of calling `Show` directly. Keep `Show` public so the Timeline signal can call it later.

**Rain.** `EnvironmentRain.StartRain` runs after every scene load and the object is `DontDestroyOnLoad`, so Act 2 would still be raining. In `StartRain`, before creating the object, if `SceneManager.GetActiveScene().name` is `Demo_Act2_Clearing`, return. Also, if the rain object already exists from Act 1, Act 2 must stop it. Add `EnvironmentRain.SetPlaying(bool)` that stops emission and mutes the loop, and call it from a tiny `SceneWeather` on Act 2 (`playing = false`) and Act 1 (`playing = true`) in `Start`. One object, two scenes, no new weather system.

**Cold read, already free.** `PlayerBreath` is already on the player. Leave it. The blue volume and the breath sell the cold. Do not add a temperature stat. The sticks are the survival task.

### Check

- Loading Act 2 from the door shows Ch44, empty hands, no health bars.
- E on a stick increments `1/3`, `2/3`, `3/3`. A fourth E does not exist.
- E on the pit before 3 does not light it.
- At 3, the light turns on and the end panel blocks movement.
- Act 1 still has rain. Act 2 does not.

---

## Phase 4 — Menu, objective, end

**Done when:** Play from `Demo_MainMenu`. Start runs the whole demo to the end card. Back returns to the menu. A person who did not build it can finish without the editor.

### To-do

1. Build the menu scene and make it scene 0.
2. Replace the temporary texts with one objective banner.
3. Hook the end card’s button to the menu.
4. Play the full path three times and write down the clock.

### How

**Menu scene.** `Assets/Game/Scenes/Demo/Demo_MainMenu.unity`.

Canvas (Screen Space Overlay):

- Canvas Scaler: Scale With Screen Size, reference `1920×1080`, match `0.5`.
- EventSystem with **Input System UI Input Module**. The project is on the new Input System only. The old Standalone Input Module will not click.
- Full-screen Image, near-black `#14120f`.
- TMP title. The string comes from the copy table so a real title is a one-line change.
- Button **Start**. `MainMenu.StartDemo()` → `SceneManager.LoadScene("Demo_Act1_Yard")`.
- Button **Quit**. `Application.Quit()`. In the editor, also `UnityEditor.EditorApplication.isPlaying = false` inside `#if UNITY_EDITOR`.

`MainMenu.cs` is about twenty lines. No fade asset, no options screen.

**Build Settings.** Enable, in this order:

0. `Demo_MainMenu`
1. `Demo_Act1_Yard`
2. `Demo_Act2_Clearing`

Disable `SampleScene`, `Demo_01`, and `Demo_01_Night` in the build list. They stay in the project.

**Objective banner.** One TMP object in each gameplay scene, same place, top center. `ObjectiveBanner.Set(string line)` is static or a singleton in the scene. Call sites:

| Moment | Line id |
|---|---|
| Act 1, control on | `him.walk` |
| Zombie `Killed` event | `him.door` |
| Act 2, control on | `her.cold` |
| Each stick | `her.wood` with the count |
| Count hits 3 | `her.light` |
| Fire `Lit` | hide the banner |

Put the strings in `DemoCopy`, a static class, matching the table at the end of this document. Gameplay code uses the ids. Wording changes happen in `DemoCopy` only.

Delete the Phase 2 one-off subtitle once this banner shows the wake line.

**End card button.** `SceneManager.LoadScene("Demo_MainMenu")`. If rain or the theme used `DontDestroyOnLoad`, loading the menu must not stack a second copy. Both of those scripts already refuse a second instance. Call `EnvironmentRain.SetPlaying(false)` when the menu loads so the menu is quiet of rain. Theme can keep playing.

**Cursor.** Gameplay locks the cursor (the current camera look already does this on play). The menu and the end card must unlock it and show it: `Cursor.lockState = CursorLockMode.None` and `Cursor.visible = true`. When Act 1 starts, lock it again. Do this in `MainMenu`, `DemoEnd`, and `DemoLoadout` so each screen owns its cursor.

**HUD.** Him’s instance: disable the `Vitals` child the same way as her, so the demo screen is the objective line and, when needed, the interact prompt. Keep `DeathPanel`.

### Check

This is the first real playtest. Do it cold, from the menu, three times.

- Start is one click.
- He never needs keys 1–4.
- The objective line matches what he should do now.
- The end button returns to the menu and Start works a second time.
- Write the three times down in a comment at the top of `DemoCopy` or in a note next to this file. If any run is over 5:00, shorten by moving the zombie or a stick. Do not start Phase 5 on a path that is already too long.

---

## Phase 5 — Cinematic cameras

**Done when:** Each camera move plays with input off, then control returns on the third-person camera. The door move is the scene change. No new camera math was written.

The shot list below is a default so work is not blocked. Reordering shots is dragging clips on the Timeline. Code stays put.

### To-do

1. Add the opening Timeline in Act 1.
2. Add the door Timeline and play it from the door instead of loading the scene immediately.
3. Add her opening Timeline.
4. Add the fire Timeline and show the end card from its signal.
5. Watch all four once and adjust clip lengths only.

### How

The gameplay camera is already a Cinemachine Camera named `Third Person Aim Camera` with **Cinemachine Third Person Follow** and **Cinemachine Third Person Aim**. Leave that component set in place. Cinematic shots are extra Cinemachine Cameras, not a new controller.

**Brain.** One `CinemachineBrain` on Main Camera. Default blend: Ease In Out, about `1.2` seconds. That blend is the “camera transition.”

**Priorities.** Gameplay camera priority `10`. Every cinematic camera priority `0`. While a Timeline Cinemachine clip plays, the track forces that shot. When the director stops, priority `10` becomes the view again.

**Create a shot.** Empty object, add **Cinemachine Camera**. Set Follow and Look At only if the shot should stick to him. For locked establishing shots, leave Follow empty and place the transform by hand. Do not add `CinemachineOrbitalFollow` or `CinemachineDeoccluder`. The third-person rig already handles collision through `AvoidObstacles`, and the setup script removes those other components on purpose.

**Timeline.** **Window → Sequencing → Timeline.** On an object `Director_Act1Opening`:

- Create `Assets/Game/Timelines/Act1_Opening.playable`
- **Cinemachine Track**, bind the Brain
- Drag cinematic cameras onto the track as shots
- **Animation Track** on a full-screen black `Image`’s `CanvasGroup`, key `alpha` from `1` to `0` over the first `0.6` s. The fade is Timeline, not a fader script
- **Signal Track** on the last frame. Create `Assets/Game/Timelines/Signals/EnableControl.signal`. Add a **Signal Receiver** on the director. The signal calls `Playgate.EnableControl`

`Playgate.cs`:

- `Awake`: `PlayerInput.DeactivateInput()` and keep the cursor locked
- `EnableControl`: `PlayerInput.ActivateInput()`, `ObjectiveBanner` shows `him.walk`
- The gameplay third-person camera is what you see after the track ends

`PlayableDirector.playOnAwake` on for both openings. Extrapolation: Hold.

**Act 1 opening, default, about 14 s**

| Time | Shot |
|---|---|
| 0.0–0.6 | From black |
| 0.6–4.0 | `VCam_YardWide`, high corner of the yard, rain in frame |
| 4.0–8.0 | Blend to `VCam_HimClose`, chest-height, he is in tired idle |
| 8.0–12.5 | Blend to a camera just behind the gameplay shoulder |
| 12.5 | Signal `EnableControl`. Track ends. Third Person Aim Camera takes over |

He is standing. He is not lying down. Reason, so we do not lose a day: `PlayerController` writes the Animator every frame (`Speed`, tired, pistol). A get-up clip on that same Animator fights the controller. The tired idle we already have, plus the camera move from the yard to his face, reads as waking up. A real get-up is an optional extra in Phase 6, behind a four-line pause in the controller.

**Door, the scene-change camera.** `Assets/Game/Timelines/Act1_Door.playable`, about 4 s. `DoorGate` no longer calls `LoadScene` itself. It swings the hinge, deactivates input, and plays this director.

| Time | Shot |
|---|---|
| 0.0–1.6 | `VCam_DoorSide`, framed on the door as it swings |
| 1.6–3.2 | Blend to `VCam_ThroughDoor`, placed on the far side, looking back at him |
| 3.2–4.0 | Black `CanvasGroup` alpha to `1` |
| 4.0 | Signal `LoadHerScene` → `SceneManager.LoadScene("Demo_Act2_Clearing")` |

This is the cinematic camera transition in the demo. If the shots feel wrong later, change cameras on this track. Keep the signal at the end.

**Act 2 opening.** `Assets/Game/Timelines/Act2_Opening.playable`, about 9 s. Same `Playgate` pattern. Her `PlayerInput` starts deactivated.

| Time | Shot |
|---|---|
| 0.0–0.5 | From black |
| 0.5–3.5 | `VCam_Trees`, through the nearest branches toward her |
| 3.5–7.0 | `VCam_HerClose` |
| 7.0 | Signal enables control and shows `her.cold` |

**Fire.** `FirePit.Lit` plays `Assets/Game/Timelines/Act2_Fire.playable` instead of showing the card immediately. Input off at the start.

| Time | Shot |
|---|---|
| 0.0–2.5 | `VCam_FireWide` |
| 2.5–5.5 | Blend closer, `VCam_FireClose` |
| 5.5 | Signal `DemoEnd.Show` |

**While a timeline plays.** `PlayerController` still applies gravity, so he stays on the ground. Input is off, so he does not walk out of frame. The third-person look script can keep running. It will not show until the gameplay camera is active again.

### Check

- Mashing WASD during an opening does not move him.
- After the signal, mouse look and WASD work, and the camera is the shoulder camera, not stuck on a cinematic camera.
- The door cut happens after the blend, not on the same frame as E.
- Skipping none of the timelines, add their seconds to the Day 2 clock. If the total crosses 4:30, shorten the wide shots before touching gameplay.

---

## Phase 6 — Animation

**Done when:** She bends down for a stick, he still shoots without an extra gun clip, and both of them idle tired. Gameplay speed and the capsule are unchanged.

### To-do

1. Confirm tired idle is what you see when energy is 0. Fix only the blend if it is not.
2. Add one Humanoid pickup clip and a state on `MaleAnimator`.
3. Play that state from the stick, with input off for the length of the clip.
4. Only if the opening still feels like he was already standing: add the optional get-up, behind `HoldAnimator`.

### How

**Tired idle.** Already wired: `PlayerVitals.IsEnergyLow` → `PlayerController` sets the `Tired` float → `MaleAnimator`. With regen at `0` and energy at `0` he should settle into `Remy@Tired-Idle` while standing still, and walk when he moves. Watch both characters. If the blend tree’s tired corners are still the normal walk (the architecture note says the tired walk corners reuse the fresh clips), leave that. A tired idle plus a normal careful walk is the right demo. Do not author a new walk cycle.

**Pickup clip.**

- Get one Humanoid FBX of a person picking an object up off the ground (Mixamo “Picking Up” or any clip with the same shape). Import **without** relying on its skin.
- Rig: Animation Type **Humanoid**, Avatar Definition **Copy From Other Avatar**, source avatar = the male FBX’s avatar (`Assets/Game/Models/Characters/LowPolyMale/Male.fbx`).
- Animation: Loop Time **off**, Root Transform Rotation and Position Y **Bake Into Pose**, so she does not drift out of the capsule.
- On `MaleAnimator.controller`, add state `Pickup` with that clip.
- Transition `Blend Tree → Pickup` on trigger `Pickup`, Has Exit Time off, duration about `0.1`.
- Transition `Pickup → Blend Tree` Has Exit Time on, exit time `0.9`, duration `0.15`.
- `PlayerController` sets floats every frame and does **not** force the state back to the blend tree every frame, so this state can play. Do not add a second animator controller. Her prefab uses the same one, so she gets the clip for free.

`StickPickup.Interact`:

- Deactivate input
- `animator.SetTrigger("Pickup")`
- Disable the stick mesh immediately, or on an **Animation Event** named `FinishStick` placed on the frame her hand meets the ground. Animation Events are part of the clip import. `FinishStick` calls `StickBag.AddOne` and destroys the pickup.
- A coroutine or the animation event’s last frame calls `PlayerInput.ActivateInput()`

If the event is missed, a coroutine of `clip.length` must still add the stick and return input. Missing an event should not soft-lock her.

**Shoot.** No new clip. `Remy@Pistol Idle`, the slide kick in `HeldItemVisual`, and the tracer already read as a shot at this distance. A full arms override can wait past this demo.

**Door.** The door hinge moves. He does not need a push animation.

**Optional get-up.** Do this only if, after Phase 5, the opening still feels wrong.

- Import one Humanoid “Getting Up” clip the same way as pickup. Root motion baked into pose.
- Add `public bool HoldAnimator { get; set; }` on `PlayerController`. At the top of `HandleAnimation`, if `HoldAnimator` is set, return before any `SetFloat` / `SetBool`.
- Opening Timeline: signal at frame 0 calls `HoldAnimator = true`. Animation track on his Animator plays the get-up. Final signal calls `HoldAnimator = false` and then `EnableControl`.
- The capsule stays upright the whole time. The mesh may leave the capsule during the lie-down frames. Frame the close camera so that reads as him on the ground, not as a bug in the yard.
- If the pop at the end is ugly, drop the get-up and return to the tired-idle opening. Do not write a root-motion driver.

### Check

- Taking a stick plays the bend, the count goes up, and she can walk afterward.
- Interrupting with movement keys does nothing until the clip ends.
- Him’s pistol walk still works in the gym prefab (trigger `Pickup` is simply unused there).
- No change to `walkSpeed`.

---

## Phase 7 — Fire and blood

**Done when:** The pit has a particle fire and a flickering light. A hit on the zombie spawns a blood burst and a stain on the ground. A miss spawns neither.

### To-do

1. Build `Campfire.prefab` and parent it under `FireVisual`.
2. Add the URP Decal feature to the PC renderer.
3. Build `BloodHit.prefab`.
4. Spawn it from `PistolShot` only when the ray actually hits the zombie.

### How

**Fire prefab.** `Assets/Game/Prefabs/VFX/Campfire.prefab`. Three Particle Systems, shader `Universal Render Pipeline/Particles/Unlit`. Reuse the smoke texture already in `Assets/Imported/Industrial/Particles/Smoke` if the material is compatible. If it is a Built-in material, make a new URP particle material and only borrow the texture.

| System | Role | Starting point |
|---|---|---|
| `Flames` | Cone, pointing up, length about `0.8` m | Start lifetime `0.4–0.7`, start speed `0.4`, start size `0.35` down to `0.05` over lifetime, color gradient from `#ffb15a` through `#ff4a1a` to alpha `0`. Noise strength about `0.4`, scroll `0.3`. Rate over time `18` |
| `Smoke` | Wider cone | Lifetime `2.5`, speed `0.6`, size `0.3` to `1.2`, color grey `#6a6a6a` at alpha `0.35` fading to `0`. Rate `8` |
| `Sparks` | Small, gravity `0.4` | Lifetime `0.6`, size `0.03`, color `#ffd27a`, rate `10`, world simulation |

Child Point Light: color `#ff7a3c`, intensity `4`, range `9`. Add the existing `TorchFlicker` and point it at this light. That script already owns flicker. Do not write a second one.

Child AudioSource, loop, play on awake, spatial blend `1`, volume low. Leave the clip empty if we do not have a fire recording yet. Drop a clip in later without code.

`FirePit` enables this whole prefab. Delete the orange cone from Phase 3.

**Blood.** First enable decals:

- Open `Assets/Settings/PC_Renderer.asset` in the inspector.
- **Add Renderer Feature → Decal**.
- Technique: Automatic or Screen Space. The PC renderer is deferred (`m_RenderingMode: 2`), which is a good fit.
- The pipeline global settings already point at a default decal material. Duplicate that material to `Assets/Game/Materials/VFX/BloodDecal.mat`. Tint it `#6b1014`. Use a soft round alpha texture. A 64×64 white blur on black is enough. Assign it as the decal’s base map.

`Assets/Game/Prefabs/VFX/BloodHit.prefab`:

- Particle System, **Stop Action: Destroy**, so the object cleans itself up. Burst `16` at time `0`, no looping. Lifetime `0.35`, gravity modifier `1`, start speed `1.5`, start size `0.05–0.12`, color `#8d1518`, world simulation, shape hemisphere.
- Child **Decal Projector**, size about `0.45 × 0.45 × 0.4`, material `BloodDecal`, pivot so it projects along the hit normal.

In `PistolShot.Fire`, the loop already stops on the first solid hit. Where it calls `zombie.Kill()`, also:

```csharp
if (bloodHitPrefab != null)
    Instantiate(bloodHitPrefab, hits[i].point, Quaternion.LookRotation(hits[i].normal));
```

Serialized field `bloodHitPrefab`. Assign the prefab on the player. A shot that hits a wall spawns nothing. One burst per kill. No blood on the mesh, no wound state.

### Check

- Fire is off until the third stick.
- Light flickers. Flames are not a flat billboard the size of a tree. Scale in the prefab until the flames sit inside the stone ring.
- Shoot the zombie: burst at the body, stain on the ground, death clip still plays.
- Shoot the sky: no blood.
- Mobile renderer is out of scope. This demo is the PC renderer.

---

## Phase 8 — Snow

**Done when:** Props in frame wear a thin white cap. The ground blanket is lower and darker where someone has walked. Walking under a roof does not wipe the snow off that roof. Her scene has falling snow and no rain.

Unity does not ship an interactive snow component. URP also does not honor the old `Camera.Render()` footprint trick, so we do not build an overhead camera. The look is three built-in pieces glued by one blit:

- White materials on caps (URP Lit)
- A dense mesh for the ground blanket, displaced in Shader Graph
- A RenderTexture of footprints, painted with `Graphics.Blit`

### To-do

1. White caps on the props the player actually sees. Budget 12 in the yard, 8 in the clearing.
2. Create the blanket mesh.
3. Create the stamp shader and `SnowField`.
4. Stamp from the existing footstep tick.
5. Shader Graph the blanket so footprints sink.
6. Falling snow prefab in Act 2 only.

### How

**Caps.** For each barrel, crate, wall top, and log that the opening camera or the walk will see: child cube, local scale `(width, 0.04, depth)`, sitting on the top face. Material `Assets/Game/Materials/VFX/SnowCap.mat`: URP Lit, base color `#d5dde4`, smoothness `0.35`, metallic `0`.

Caps do **not** use the trail. The trail is a flat map of the ground. A roof that sampled it would lose its snow when someone walked underneath. Static caps are the object snow. The interactive part is the ground under the feet.

**Blanket mesh.** Menu **Tools → Demo → Create Snow Blanket** in a new editor script `Assets/Game/Scripts/Editor/CreateSnowBlanketMesh.cs`.

- Width `40`, depth `24`, step `0.3` m. That is about 134 × 80 vertices. Fine for one clearing.
- Build a `Mesh`, vertices on XZ, normals up, UVs unused (the shader uses world position).
- `AssetDatabase.CreateAsset` to `Assets/Game/Models/Demo/SnowBlanket_40x24.asset`.
- Make a second asset `SnowBlanket_24x24` for her clearing, or one mesh reused at different scales **only if** the world-space shader still matches. Prefer two meshes whose corner sits on the `SnowField` origin, scale `1`.

Place the mesh on an object `SnowBlanket`. No collider. The real ground collider stays the thing they walk on. The blanket is visual, sitting about `0.01` m above the ground. Footsteps will not physically sink. The vertices under the feet drop, which is the read we want. Max drop is `0.045` m (“a little layer”).

**Stamp shader.** `Assets/Game/Shaders/SnowStampBlit.shader`. Full file:

```hlsl
Shader "Demo/SnowStampBlit"
{
    Properties
    {
        _MainTex ("Trail", 2D) = "black" {}
        _Point ("UV xy, radius z", Vector) = (0.5, 0.5, 0.02, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Point;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half trail = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).r;
                float dist = distance(i.uv, _Point.xy);
                half stamp = saturate(1.0h - (half)(dist / max(_Point.z, 0.0001)));
                stamp *= stamp;
                half next = saturate(trail + stamp);
                return half4(next, next, next, 1);
            }
            ENDHLSL
        }
    }
}
```

**SnowField.cs** on the blanket object:

- Serialized `Vector2 origin` (world XZ of the mesh corner), `Vector2 size`, `float radiusUv = 0.018` (about `0.7` m on a 40 m blanket).
- `Awake`: create two `RenderTexture`s, `1024×1024`, `RenderTextureFormat.ARGB32`. A single-channel format fights the blit shader, so keep four channels. Filter bilinear, wrap clamp. Clear both to black (`RenderTexture.active`, then `GL.Clear`).
- Material instance of the blanket shader. Set origin, size, depth `0.045`, and the current trail texture.
- `public static Action<Vector3> Stamp;`
- On enable, `Stamp += Paint`. On disable, `Stamp -= Paint`.
- `Paint(worldPos)`: convert XZ to UV with `(worldPos - origin) / size`. If UV is outside `0–1`, return. Set `_Point` on the blit material. `Graphics.Blit(front, back, blitMaterial)`. Swap front and back. Assign the new front to the blanket material’s trail slot.
- Call `Paint` once per foot, not every frame.

**Hook.** In `RainFootsteps.PlayStep`, after the audio plays:

```csharp
SnowField.Stamp?.Invoke(transform.position);
```

Steps and footprints stay on the same stride (`WalkStride` `2.05` m). No second step detector.

**Shader Graph.** `Assets/Game/Shaders/SnowBlanket.shadergraph`. Create → Shader Graph → URP → **Lit Shader Graph**.

Graph settings: Opaque, Render Face Front.

Properties:

| Property | Type | Value |
|---|---|---|
| `Trail` | Texture2D | black |
| `Origin` | Vector2 | filled at runtime |
| `Size` | Vector2 | filled at runtime |
| `Depth` | Float | `0.045` |
| `SnowColor` | Color | `#d5dde4` |
| `GroundColor` | Color | `#2a261f` |

Vertex stage:

- Position, Space **World**
- Split XZ
- `uv = (xz - Origin) / Size`
- Sample `Trail` at `uv` (default sampler)
- `sink = Depth * trail.r`
- Subtract `sink` from world Y
- **Transform** World → Object
- Connect to the Vertex **Position** block

Fragment:

- Lerp `SnowColor` → `GroundColor` by `trail.r`. Connect to **Base Color**
- Smoothness: lerp `0.4` → `0.08` by `trail.r`

Assign the graph to `Assets/Game/Materials/VFX/SnowBlanket.mat` and put that material on the mesh. `SnowField` overwrites origin, size, depth, and the trail texture on a runtime instance of the material (`renderer.material`), so the asset stays shared.

**Falling snow.** `Assets/Game/Prefabs/VFX/FallingSnow.prefab`. One Particle System:

- Shape: box covering the clearing (about `24 × 8 × 24`), placed `6` m up
- Gravity modifier `0.02`, start speed `0.4`, start size `0.03`, lifetime `8`
- Max particles `700`, rate `80`
- Simulation space World
- Material: URP Particles Unlit, white, a soft circle
- Collision **off**
- No script

Drop the prefab in Act 2 only. Act 1 keeps `EnvironmentRain` (sleet is the yard: rain plus the white caps and the blanket). Act 2 is snow, and Phase 3 already turned the rain loop off there.

### Check

- Walk a line. A softer, lower, darker track follows the feet. It stays after you leave.
- Stand still. The blanket does not melt.
- A barrel cap stays fat after you walk beside the barrel.
- Act 2: flakes, no rain sound, no rain streaks.
- Act 1: rain still on, blanket still sinks.
- Frame time: if the blanket hurts the editor, drop the texture to `512` and the mesh step to `0.45` m before doing anything else.

---

## Phase 9 — Camera, motion, and the clock

**Done when:** A full run from the menu is under 5:00, the shoulder camera does not jab into walls, and the notes for that run are written at the bottom of this file.

### To-do

1. Tune Third Person Follow numbers only.
2. Watch both acts for feet, door, fire, and prompts.
3. Time three clean runs.
4. If a run is long, cut in the order listed below.
5. Stop. No new beats.

### How

**Camera.** Select `Third Person Aim Camera`. The component is **Cinemachine Third Person Follow**. Fields that already exist, and the values the setup script wrote:

| Field | Currently | Demo aim |
|---|---|---|
| Damping | `(0.08, 0.15, 0.2)` | If the yard camera shakes on small steps, raise X and Z slightly, around `0.15` and `0.25`. If it feels late, leave it |
| Shoulder Offset | set by the setup | Keep him on the left of frame so the pistol barrel stays visible. Nudge, do not redesign |
| Vertical Arm Length | set by the setup | Eye line roughly at his shoulders |
| Camera Distance | set by the setup | Close enough to read the door handle area. About the current value |
| Camera Side | `1` | Leave |
| Avoid Obstacles → Enabled | on | Leave on |
| Camera Radius | `0.2` | If the lens enters a hangar wall, raise toward `0.35` |
| Damping Into Collision | `0` | Leave, so it comes forward immediately at a wall |
| Damping From Collision | `0.25` | Leave |
| Ignore Tag | `Player` | Leave |

`CinemachineThirdPersonLook` owns the mouse. `mouseGain` is `0.45`. Change it only if the yard feels too fast or too slow. Do not add a second look script.

Pistol camera `Pistol Aim Camera` stays for the fight. It is already in the rig.

There is also a **Third Person Aim** aim component (`Aim Distance` `80`). Leave it so the barrel and the crosshair agree. `HeadLook` already turns the head and the gun. Do not add arm IK.

**Motion.**

- Pickup does not move the capsule.
- Get-up, if it was added, ends on the feet, not in the floor.
- Zombie death stays on the ground (`ZombieController` already holds the pose).
- Door swing does not push him through a collider.

**Volume.** Act 1 keeps the duplicated night profile. Act 2 keeps the cold profile from Phase 1. If the two acts feel like different games, match exposure so the cut through the door is a change of place, not a change of brightness.

**Three runs.** From a fresh menu click, phone or a stopwatch, to the end card. Record them here when you do it:

| Run | Time | Where it dragged |
|---|---|---|
| 1 | | |
| 2 | | |
| 3 | | |

**If the time is over 5:00, cut in this order**

1. Shorten the wide cinematic shots.
2. Move the zombie closer, still out of sight from the spawn.
3. Move the furthest stick closer.
4. Drop the two background zombies.
5. Drop the optional get-up.

Do not cut the shot, the door, the three sticks, or the fire.

**Freeze.** After a good run, stop adding objects. Story changes are wording in `DemoCopy` and camera clips on the Timelines.

### Check

- Menu to end card, under 5:00, on at least two of the three runs.
- Camera never stays inside a wall.
- Rain in the yard, snow in the trees, blood on the one kill, fire at the end.
- `Player.prefab` walk speed is still `5`. SampleScene still loads.

---

## Copy table

All player-facing lines. Code reads the id. Change the words here and in `DemoCopy` together.

| Id | Text | Where |
|---|---|---|
| `title` | Cold Watch | Menu title. Replace when the story has a name |
| `start` | Start | Menu button |
| `quit` | Quit | Menu button |
| `him.walk` | I don't remember this place. | Act 1, control returns |
| `him.door` | There is a door. | Zombie killed |
| `door.shut` | The door is shut. | Looking at the door too early |
| `door.open` | Open | Looking at the door after the shot |
| `her.cold` | I don't remember. It's cold. | Act 2, control returns |
| `her.take` | Take the wood | Looking at a stick |
| `her.wood` | Wood {0}/3 | After each stick |
| `her.need` | Need more wood | Pit, before 3 |
| `her.light` | Light the fire | Pit, at 3 |
| `end` | The fire holds. | End card |
| `back` | Back | End card button |

No voiceover and no lore note on the door. The unanswered part is the story that is still open.

---

## Files this plan adds

Existing files that get a small edit:

| File | Change |
|---|---|
| `ZombieController.cs` | `IsDead`, `Killed` event |
| `PlayerController.cs` | `OnInteract` calls `PlayerUse`. Optional `HoldAnimator` in Phase 6 |
| `PlayerInventory.cs` | `NotifyChanged()` so empty hands refresh |
| `PistolShot.cs` | Spawn blood prefab on a zombie hit |
| `RainFootsteps.cs` | One stamp call |
| `EnvironmentRain.cs` | `SetPlaying`, and Act 2 does not start a new rain |

New files:

| Path | Role |
|---|---|
| `Assets/Game/Scenes/Demo/Demo_MainMenu.unity` | Start |
| `Assets/Game/Scenes/Demo/Demo_Act1_Yard.unity` | Him |
| `Assets/Game/Scenes/Demo/Demo_Act2_Clearing.unity` | Her |
| `Assets/Game/Prefabs/Demo/Player_Her.prefab` | Ch44 |
| `Assets/Game/Prefabs/VFX/Campfire.prefab` | Fire |
| `Assets/Game/Prefabs/VFX/BloodHit.prefab` | Blood |
| `Assets/Game/Prefabs/VFX/FallingSnow.prefab` | Flakes |
| `Assets/Game/Scripts/Demo/IInteractable.cs` | |
| `Assets/Game/Scripts/Demo/PlayerUse.cs` | E key lookup |
| `Assets/Game/Scripts/Demo/DoorGate.cs` | |
| `Assets/Game/Scripts/Demo/DemoLoadout.cs` | Pistol or empty hands |
| `Assets/Game/Scripts/Demo/StickBag.cs` | |
| `Assets/Game/Scripts/Demo/StickPickup.cs` | |
| `Assets/Game/Scripts/Demo/FirePit.cs` | |
| `Assets/Game/Scripts/Demo/DemoEnd.cs` | |
| `Assets/Game/Scripts/Demo/DemoCopy.cs` | The lines |
| `Assets/Game/Scripts/Demo/ObjectiveBanner.cs` | |
| `Assets/Game/Scripts/Demo/MainMenu.cs` | |
| `Assets/Game/Scripts/Demo/Playgate.cs` | Timeline signal |
| `Assets/Game/Scripts/Demo/SceneWeather.cs` | Rain on or off |
| `Assets/Game/Scripts/Demo/SnowField.cs` | Trail texture |
| `Assets/Game/Shaders/SnowStampBlit.shader` | |
| `Assets/Game/Shaders/SnowBlanket.shadergraph` | |
| `Assets/Game/Scripts/Editor/CreateSnowBlanketMesh.cs` | |
| `Assets/Game/Timelines/Act1_Opening.playable` | |
| `Assets/Game/Timelines/Act1_Door.playable` | |
| `Assets/Game/Timelines/Act2_Opening.playable` | |
| `Assets/Game/Timelines/Act2_Fire.playable` | |

---

## These stay as they are

- `PlayerController` movement, stairs, climb, jump math
- `ThirdPersonCamera.cs` (empty on purpose)
- The Cinemachine third-person components and `CinemachineThirdPersonLook`, except the numeric tweaks in Phase 9
- Hotbar rules on the shared player prefab
- Zombie sequence (crawl, stand, scream, run, death)
- SampleScene, Demo_01, Demo_01_Night, both IndustrialYard scenes
- Adding packages

---

## After the freeze

When the 5-minute run is real, the open story can land without a new system: names in `DemoCopy`, different Timeline shots, a different FBX on `Player_Her`. A follow-up that reunites them is a new act, not a change to this one.
