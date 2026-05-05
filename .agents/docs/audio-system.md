# Audio System Documentation

## Purpose

The audio system owns background music selection, global mixer volume application, and reusable local sound-effect playback through scene or prefab-wired `AudioSource` components.

The audio system is not responsible for gameplay decisions, UI flow, settings persistence, VFX completion, pooling, or scene loading. Those systems trigger audio through narrow interfaces or local `AudioClipPlayer` components while keeping their own state ownership.

## Reading Map

- Primary code locations:
  - `Assets/Scripts/Audio/`
  - `Assets/Scripts/ReflexDI/BootLoader.cs`
  - `Assets/Scripts/Settings/AudioVolumeSetting.cs`
  - `Assets/Scripts/UI/Settings/AudioVolumeOption.cs`
- Common consumers:
  - `Assets/Scripts/UI/Death/PlayerDeathPresenter.cs`
  - `Assets/Scripts/UI/Skills/SkillUpgradePresenter.cs`
  - `Assets/Scripts/UI/ButtonsAudioClipPlayer.cs`
  - `Assets/Scripts/Player/PlayerManager.cs`
  - `Assets/Scripts/Player/PlayerDamagedHandler.cs`
  - `Assets/Scripts/Enemies/Enemy.cs`
  - `Assets/Scripts/Enemies/EnemyDeathHandler.cs`
  - `Assets/Scripts/LevelSystem/Exp/ExpParticle.cs`
  - `Assets/Scripts/Skills/PlayerSkills/`
- Related docs:
  - `.agents/docs/settings-system.md`
  - `.agents/docs/ui-system.md`
  - `.agents/docs/level-system.md`
  - `.agents/docs/enemies-system.md`
  - `.agents/docs/skills-system.md`
  - `.agents/docs/technology-documentation.md`
- Related agents or instructions:
  - Root `AGENTS.md`
  - `.agents/docs/project-coding-standards.md`
  - `.agents/skills/document-system/SKILL.md`
  - `.agents/skills/di-integration/SKILL.md` when adding injected audio dependencies or bindings

## Architecture and Data Flow

- Core components:
  - `AudioClipConfig` is a serializable clip configuration containing an `AudioClip`, volume, pitch, and loop flag. It is used by both background music and local clip players.
  - `BackgroundAudioManager` is a scene-level service with an `AudioSource`. It maps `GameScene` values to `AudioClipConfig` entries, listens to `IGameSceneLoader.OnSceneLoaded`, and plays or continues the configured background clip for the loaded scene.
  - `AudioMixersManager` is a scene-level service that applies a volume value to the configured Unity `AudioMixer`.
  - `AudioClipPlayer` is a reusable local sound-effect component. It maps string names to arrays of `AudioClipConfig` variants, randomly selects a variant, prepares its required `AudioSource`, plays the clip, and raises `OnAudioClipFinished` after the clip length.
- Key interfaces:
  - `IBackgroundAudioManager` exposes death/default background audio mode changes.
  - `IAudioMixersManager` exposes mixer volume application.
  - `IAudioClipPlayer` exposes `Play`, `PlayOneShot`, and `OnAudioClipFinished`.
- Runtime flow:
  - `BootLoader` registers the boot-scene `AudioMixersManager` as `IAudioMixersManager` and `BackgroundAudioManager` as `IBackgroundAudioManager` into every scene container through `SceneScope.OnSceneContainerBuilding`.
  - `BackgroundAudioManager` subscribes to scene-loaded events in `OnEnable` and unsubscribes in `OnDisable`. When a scene has a configured clip, it updates loop, pitch, and volume; it only assigns and starts playback when the configured clip differs from the current source clip.
  - `PlayerDeathPresenter` resets background audio pitch to default in `Start`, then switches to death audio mode when enabling the death screen.
  - `AudioVolumeSetting` stores the mixer volume value through `AppStorage`, and `AudioVolumeOption` converts between slider values and decibels before saving and applying the setting.
  - Prefab or scene-local actors use `GetComponentInChildren<IAudioClipPlayer>()` for local effects such as player damage, enemy death, exp collection, landmine explosions, saw attacks, laser shots, and minigun shots.
  - UI skill upgrade sounds use serialized or child `AudioClipPlayer` references to play panel show, hover, and click sounds.

## Rules and Invariants

- Critical behavior rules:
  - Keep global audio services registered from `BootLoader`; do not replace them with singleton access or broad scene searches.
  - Keep local sound effects owned by the prefab or scene object that emits them. Shared gameplay systems should consume `IAudioClipPlayer` through their owning component instead of reaching into unrelated objects.
  - Preserve `BackgroundAudioManager` scene-to-clip mapping as designer-authored serialized data.
  - Preserve death audio behavior unless intentionally changing death-screen UX: default pitch is restored when `PlayerDeathPresenter` starts, and death mode lowers background pitch.
  - Preserve settings persistence semantics: `AudioVolumeSetting.GetKey()` currently stores volume under `"Volume"` and applies it through the mixer manager.
- Ordering or sequencing guarantees:
  - Background music reacts after `IGameSceneLoader.OnSceneLoaded` is raised.
  - `AudioClipPlayer.Play` cancels any pending finish callback from a previous `Play` call before scheduling the next one.
  - `EnemyDeathHandler` waits for both death VFX and death audio finish events before raising `OnCompleted`.
  - `ExpParticle.CollectExp` delays release callback until shrink animation and collection audio have both completed.
- Constraints contributors must preserve:
  - Do not edit audio scene, prefab, mixer, ScriptableObject, or meta assets directly unless the user explicitly asks and the text diff is safe to review.
  - Do not change clip names such as `"Death"`, `"Damaged"`, `"ExpCollected"`, `"Explosion"`, `"Attack"`, `"Shoot"`, `"Show"`, `"Click"`, and `"Hover"` without updating the corresponding serialized `AudioClipPlayer` configs.
  - Keep `AudioClipPlayer` on GameObjects with an `AudioSource`; the component requires one and initializes it in `Awake`.
  - Treat volume values passed to `AudioMixersManager` as mixer parameter values, not normalized UI slider values.

## Extension Points

- Safe extension areas:
  - Add new local sound effects by adding a named entry to an object's `AudioClipPlayer` config and calling `Play` or `PlayOneShot` from the owning component.
  - Add clip variants by extending the `ClipVariants` array for an existing audio name. The player randomly selects one variant each play.
  - Add new background music by adding a `GameScene` and `AudioClipConfig` entry to `BackgroundAudioManager`'s serialized scene config.
  - Add additional mixer controls by extending `IAudioMixersManager` and its implementation, then binding and consuming the new contract through Reflex.
- Required dependencies and contracts:
  - Global background and mixer access require Reflex bindings from `BootLoader`.
  - Local clip playback requires a child or same-object `AudioClipPlayer` with an assigned `AudioSource` and matching serialized clip-name config.
  - Audio settings require `AudioVolumeSetting` to be bound in `MainMenuInstaller` as `ISetting<AudioVolumeSetting, float>`.
- Testing implications:
  - Compile documentation-adjacent C# changes with `dotnet build Assembly-CSharp.csproj -p:BuildProjectReferences=false`.
  - Unity Editor validation is required for mixer exposed parameter names, `AudioSource` setup, clip assignments, clip-name config strings, prefab child references, and scene background music mappings.
  - Manual play-mode checks should cover scene transitions, death-screen pitch change, settings slider behavior, enemy death completion, exp collection release timing, and rapid repeated local sound playback.

## Integration Notes

- Upstream dependencies:
  - `BackgroundAudioManager` depends on `IGameSceneLoader` events and `GameScene` values.
  - `AudioMixersManager` depends on a serialized Unity `AudioMixer`.
  - `AudioVolumeSetting` depends on `IAudioMixersManager` and `AppStorage`.
  - Local effect playback depends on prefab or scene hierarchy placement of `AudioClipPlayer`.
- Downstream consumers:
  - `PlayerDeathPresenter` consumes `IBackgroundAudioManager`.
  - `AudioVolumeSetting` consumes `IAudioMixersManager`; `AudioVolumeOption` consumes the setting.
  - `EnemyDeathHandler` consumes `Enemy.AudioClipPlayer` to sequence death completion.
  - `PlayerDamagedHandler`, skills, exp particles, and skill upgrade UI consume local `IAudioClipPlayer` or `AudioClipPlayer` components.
- Cross-system coupling risks:
  - Changing `AudioClipPlayer.OnAudioClipFinished` timing can affect enemy pooling/release and exp particle collection.
  - Changing death background audio behavior affects UI death flow and game-over feel.
  - Changing mixer parameter names or slider conversion affects persisted settings and audio volume UX.
  - Renaming serialized audio config names can silently stop sounds if consumers still request the old string.
  - Changing `PlayOneShot` behavior can affect overlapping damage or pickup sounds.

## Known Risks and Open Questions

- Known limitations:
  - `AudioClipPlayer.GetRandomAudioClipVariantFromConfigByName` checks `config.ClipVariants` when `config` may be null, so a missing audio name can throw instead of logging and returning null.
  - `AudioClipPlayer.PlayOneShot` does not cancel previously scheduled finish callbacks, so repeated one-shot playback can raise multiple finish events.
  - `AudioClipPlayer` schedules finish callbacks with `_audioSource.clip.length`; pitch changes and looping clips are not accounted for.
  - `AudioMixersManager.SetMixerVolume` accepts a `mixerName` parameter but currently always sets the `"Volume"` exposed parameter on the main mixer.
  - `BackgroundAudioManager` uses a non-serialized `_deathAudioPitch` value of `0.6f`, so designers cannot tune death pitch in the inspector.
  - `AudioVolumeOption` uses `Mathf.Log10(_slider.value)`; the slider must not allow zero.
  - `ButtonsAudioClipPlayer` declares `RequireComponent(typeof(AudioClipPlayer))` but does not initialize `_audioClipPlayer` before use.
  - Several gameplay components find local audio through `GetComponentInChildren<IAudioClipPlayer>()`; missing child setup will fail at runtime.
- Open design questions:
  - Should local audio clip keys become constants or typed IDs to reduce string mismatch risk?
  - Should `AudioClipPlayer` distinguish completion events per play request for systems that wait on a specific clip?
  - Should mixer controls support separate music, SFX, and UI channels instead of one global `"Volume"` parameter?
  - Should death pitch be designer-configurable per scene, globally configured, or intentionally fixed?
- Suggested follow-up tasks:
  - Fix `AudioClipPlayer` missing-config handling and add guard behavior for empty variant arrays.
  - Fix `ButtonsAudioClipPlayer` initialization before relying on it as shared UI button audio.
  - Review `AudioClipPlayer.OnAudioClipFinished` semantics for one-shots, pitch, and looped clips before adding more systems that wait on audio completion.
  - Add a Unity Editor checklist for audio prefab setup: `AudioSource`, `AudioClipPlayer`, named configs, mixer routing, and expected clip lengths.
