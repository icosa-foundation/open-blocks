# Unity 6 CI and Cross-Platform XR Build Plan

Status: planning document only. This file is intentionally untracked and must not be staged or committed.

Date: 2026-09-09

## 1. Objective

Bring the Open Blocks build pipeline onto Unity `6000.6.0f1`, make its caches safe for Unity 6, and establish a deterministic build system that can target the same hardware families as Open Brush, including Android XR, without importing Open Brush-specific product behavior.

The work is divided into two tracks:

1. Stabilize the existing CI matrix after the Unity 6.6 upgrade.
2. Introduce an Open Blocks-specific build entry point and extend the platform matrix deliberately.

## 2. Confirmed baseline

1. `ProjectSettings/ProjectVersion.txt` specifies Unity `6000.6.0f1`.
2. `.github/workflows/build.yml` still specifies Unity `6000.3.18f1`.
3. CI currently invokes `game-ci/unity-builder@v4`.
4. Open Brush moved to `game-ci/unity-builder@v5` in its Unity 6 cache-fix change.
5. Open Blocks currently has no custom `BuildPipeline.BuildPlayer` entry point.
6. The workflow's `-btb-il2cpp` and `-btb-bopt Development` values are not passed to Unity and therefore have no effect.
7. Android currently uses IL2CPP and ARM64 through serialized Player Settings.
8. Android target SDK is currently selected by editing `ProjectSettings/ProjectSettings.asset` with `sed`.
9. The project already contains these relevant packages:

   1. `com.unity.xr.openxr` `1.18.0`.
   2. `com.unity.xr.androidxr-openxr` `1.4.0`.
   3. `com.unity.xr.management` `4.7.0`.
   4. Android XR transitive dependencies including XR Hands, AR Foundation, and Composition Layers.
   5. Meta platform support.

10. The current CI matrix builds Windows OpenXR, Linux 2D, macOS 2D, generic Android OpenXR, Google Play Android OpenXR, and Android 2D.
11. The current workflow restores a separate `Library/PackageCache` cache even though saving that cache is disabled.
12. The current workflow saves broad `Library` contents after any cache miss, including branch and development jobs.

## 3. Design decisions

1. Do not copy `BuildTiltBrush.cs` wholesale. Create a smaller `BuildOpenBlocks.cs` containing only Open Blocks requirements.
2. Treat hardware/runtime selection separately from product variants. Open Brush Viewer variants should not be reproduced unless Open Blocks gains an equivalent product mode.
3. Give each materially different Android runtime its own matrix entry and artifact.
4. Make build-time settings temporary. Every modified Player Setting, XR loader, OpenXR feature, define, and graphics API must be restored in `Dispose` or `finally` logic.
5. Do not rely on the last interactive Editor state to determine CI output.
6. Use Unity APIs for Player Settings instead of editing serialized YAML wherever an API exists.
7. Keep CI changes and runtime-expansion changes in separate commits so failures have a small diagnosis surface.
8. Use the unique log prefix `[OBXRBUILD]` for the new build path and its tests.
9. Do not add caching or build-script abstraction without a concrete matrix consumer.
10. Do not push implementation commits without explicit permission.

## 4. Intended platform matrix

| Matrix entry | Runtime purpose | Output | Target SDK | XR loader/profile | Initial priority |
|---|---|---:|---:|---|---:|
| Windows OpenXR | Desktop PC VR | Player directory/archive | N/A | OpenXR, Standalone settings | Existing |
| Linux 2D | Desktop non-XR | Player directory/archive | N/A | XR disabled | Existing |
| macOS 2D | Desktop non-XR | App/archive | N/A | XR disabled | Existing |
| Android OpenXR | Generic sideload build for compatible mobile XR devices | APK | API 34 initially | OpenXR, cross-device Android feature set | Existing, revise |
| Android XR | Google Play build for Android XR hardware | AAB | API 35 or current store requirement | OpenXR plus Android XR build profile/features | New, high |
| Android Meta Quest | Meta store/sideload build | APK | API 34 until Meta validation changes | OpenXR plus required Meta feature/build hooks | New, high |
| Android 2D | Mobile non-XR mode | APK | API 34 initially | XR disabled | Existing |
| iOS/Zapbox | Open Brush hardware-parity candidate | Xcode/iOS artifact | N/A | Zapbox loader | Discovery gate |

Notes:

1. Android SDK requirements are policy inputs and must remain matrix values, not hard-coded inside the build method.
2. Android XR and Meta Quest must remain distinct even though both use OpenXR.
3. The generic Android OpenXR artifact should contain only features proven safe across its intended Quest/Pico-compatible runtimes.
4. iOS/Zapbox requires a separate dependency, licensing, signing, and product-fit review before it is added to active CI.

## 5. Phase A: stabilize the existing Unity 6 CI

### 5.1 Align Unity and GameCI

1. Change workflow `UNITY_VERSION` to `6000.6.0f1`.
2. Change `game-ci/unity-builder@v4` to `game-ci/unity-builder@v5`.
3. Confirm the retry wrapper accepts the unchanged multiline `with` input format.
4. Confirm the required Unity 6.6 Linux editor images exist for every current target.
5. Preserve existing build names and artifact paths during this phase.

Acceptance criteria:

1. Workflow and `ProjectVersion.txt` name the same Unity release.
2. YAML validation succeeds.
3. One uncached existing matrix build reaches Unity compilation using `6000.6.0f1`.
4. The job log confirms `unity-builder@v5` is the invoked action.

### 5.2 Correct Library caching

1. Define the cache whitelist on the cleanup step or at job scope:

   `DataStore ArtifactDB SourceAssetDB PlayerDataCache ShaderCache ShaderCache.db`

2. Never place the whitelist only on a preceding step because step-scoped environment variables do not propagate.
3. Before saving, move only whitelisted paths into a temporary directory.
4. Remove the remaining `Library` directory and replace it with the whitelist directory.
5. Save Library caches only when all of these conditions are true:

   1. The Library restore was a miss.
   2. The ref is `refs/heads/main`.
   3. The build is not a development flavor.
   4. The Unity build step completed successfully.

6. Keep the cache key based on runtime cache group and exact Unity version.
7. Add the packages lock hash to the Library key only if testing demonstrates that dependency changes are not already handled safely by Unity's retained databases.
8. Disable the separate `Library/PackageCache` restore and save steps initially.
9. Retain bounded disk-usage diagnostics without printing an unbounded file listing.

Acceptance criteria:

1. A cache-save job contains only the approved paths.
2. A branch or pull-request job does not save a new Library cache.
3. A development flavor does not save a new Library cache.
4. A second main-branch-equivalent test restores the cache and completes compilation.
5. Restoring a cache does not reproduce Unity's post-upgrade build failure.

### 5.3 Use supported Android SDK inputs

1. Replace numeric SDK matrix values with Unity enum values such as `AndroidApiLevel34` and `AndroidApiLevel35`.
2. Pass `androidTargetSdkVersion: ${{ matrix.androidTargetSdkVersion }}` to `unity-builder@v5`.
3. Remove the `Set Android target SDK` step that edits `ProjectSettings.asset`.
4. Confirm the resulting APK or AAB target SDK using Android build tools rather than relying only on workflow input echoing.

Acceptance criteria:

1. The checkout remains clean after the build's SDK-selection phase, except for known generated files produced inside the builder container.
2. API 34 entries produce target SDK 34 artifacts.
3. API 35 entries produce target SDK 35 artifacts.

### 5.4 Resolve dead workflow parameters

1. Remove `extraoptions: -btb-il2cpp` from the existing matrix during the stabilization phase because Android IL2CPP is already serialized.
2. Do not claim development builds are supported until the new build method applies `BuildOptions.Development`.
3. Temporarily either disable the development-flavor expansion or label it clearly as unsupported.
4. Preserve the `[CI BUILD DEV]` trigger contract only if Phase B is scheduled immediately afterward.

Acceptance criteria:

1. Every matrix field is consumed by a workflow expression or documented as future-only.
2. CI job names do not imply a development player when the player was built without `BuildOptions.Development`.

## 6. Phase B: create the Open Blocks build entry point

### 6.1 File and public contract

1. Add `Assets/Editor/BuildOpenBlocks.cs` in an Editor-only assembly context.
2. Expose one static CI entry point, for example `BuildOpenBlocks.CommandLine`.
3. Define an explicit runtime enum rather than accepting arbitrary display strings. Initial values should cover:

   1. `Monoscopic`.
   2. `OpenXR`.
   3. `AndroidXR`.
   4. `MetaQuest`.
   5. `Zapbox` only after the discovery gate passes.

4. Parse only documented arguments, including:

   1. Target platform.
   2. Runtime.
   3. Output path.
   4. Development-build flag.
   5. Android target SDK.
   6. Android APK versus AAB selection when not fully handled by GameCI.
   7. Optional build stamp/version fields already used by Open Blocks.

5. Reject unknown values and incompatible target/runtime combinations with a `BuildFailedException`.
6. Redact secrets from all command-line logging.
7. Prefix build-method logs with `[OBXRBUILD]`.

Acceptance criteria:

1. Argument parsing has EditMode tests for valid, missing, malformed, and conflicting values.
2. Unsupported combinations fail before modifying project settings.
3. No keystore password, token, or secret is written to logs.

### 6.2 Temporary settings scopes

Implement small disposable scopes instead of one large mutable procedure:

1. `TemporaryPlayerSettings`:

   1. Android target SDK.
   2. Android minimum SDK where runtime-specific.
   3. Android application entry point.
   4. Resizable activity.
   5. Bundle version and application identifier only where Open Blocks requires an override.

2. `TemporaryScriptingBackend`:

   1. Preserve the current backend.
   2. Require IL2CPP for Android runtime entries.
   3. Restore the original backend after the build.

3. `TemporaryXrLoader`:

   1. Preserve the complete loader list and `InitManagerOnStart` state.
   2. Assign OpenXR for OpenXR, Android XR, and Meta Quest entries.
   3. Disable XR initialization for monoscopic entries.
   4. Restore the original list on success or failure.

4. `TemporaryOpenXrFeatures`:

   1. Record each feature's enabled state.
   2. Enable the runtime's required feature set by concrete feature type where duplicate IDs are possible.
   3. Restore every changed feature afterward.

5. `TemporaryGraphicsApis`:

   1. Preserve the target's ordered graphics API list.
   2. Apply only a validated runtime-specific list.
   3. Restore the original ordering afterward.

6. `TemporaryDefines`:

   1. Preserve symbols through `NamedBuildTarget` APIs.
   2. Apply `XR_DISABLED` to monoscopic builds if still required by source compilation.
   3. Add an Android XR-specific symbol only when code genuinely needs compile-time branching.
   4. Restore the original symbols afterward.

Acceptance criteria:

1. A forced build failure still restores all temporary settings.
2. A settings snapshot before and after each test build is identical except for intentionally generated artifacts.
3. All new code uses current Unity 6 APIs, including `NamedBuildTarget` where applicable.

### 6.3 Build execution

1. Resolve enabled scenes deterministically from Editor Build Settings or an explicit reviewed list.
2. Construct `BuildPlayerOptions` with an explicit location, target, and options.
3. Apply `BuildOptions.Development` only for the development flavor.
4. Add debugging/profiler options only when separately requested by the flavor contract.
5. Call `BuildPipeline.BuildPlayer` once.
6. Treat any non-success `BuildResult` as a `BuildFailedException` and include the bounded error summary.
7. Ensure the output location matches the existing upload and publication jobs.

Acceptance criteria:

1. Release and development builds differ in `BuildOptions` as intended.
2. Failed builds return a non-zero Unity process exit.
3. Upload steps find exactly one expected primary artifact.

## 7. Phase C: deterministic XR runtime profiles

### 7.1 Inventory before selecting features

1. Record the enabled Android and Standalone OpenXR features after the Unity 6.6 migration.
2. Map each feature to an actual Open Blocks capability or platform requirement.
3. Identify duplicate feature IDs and select concrete C# types when necessary.
4. Separate required features from optional capabilities negotiated at runtime.
5. Confirm whether Meta support comes from the Unity OpenXR Meta Quest feature, Meta packages, or both.

### 7.2 Generic Android OpenXR profile

1. Enable the OpenXR loader.
2. Retain interaction profiles used by current Quest and Pico controllers.
3. Retain hand tracking only if the Android OpenXR artifact exposes that feature.
4. Retain passthrough, composition layers, and vendor extensions only when tested across intended devices.
5. Avoid enabling Android XR build-profile features in the generic APK unless compatibility testing requires them.

### 7.3 Android XR profile

1. Enable the OpenXR loader.
2. Enable the Android XR support/build-profile feature.
3. Configure Android GameActivity.
4. Enable resizable activity.
   5. Enforce API level 26 as the Android XR minimum SDK required by the installed package on Unity 6.6.
6. Review and selectively enable relevant AR Foundation providers:

   1. Session.
   2. Camera.
   3. Planes.
   4. Anchors.
   5. Raycasts.
   6. Occlusion.
   7. Meshing, faces, or bounding boxes only when Open Blocks consumes them.

7. Review hand tracking, hand mesh, composition layers, display utilities, and foveated rendering against application use.
8. Produce an AAB for Google Play.

### 7.4 Meta Quest profile

1. Enable the OpenXR loader.
2. Enable the Meta Quest OpenXR build feature and required build hooks.
3. Preserve Open Blocks' existing asymmetric-projection behavior unless device testing identifies a reason to change it.
4. Use API 34 while Meta validation requires it; keep the value configurable.
5. Produce a separate APK and keep its publication path independent from generic Android OpenXR.

### 7.5 Monoscopic profile

1. Disable XR initialization deterministically.
2. Confirm whether XR packages can remain installed while `XR_DISABLED` controls source inclusion.
3. Stop removing packages per matrix job if builds remain correct with packages installed.
4. If package removal remains necessary, include the effective package set in the cache key and verify the packages lock is restored after the job.

Acceptance criteria for Phase C:

1. Runtime profiles produce the same loader and feature selection from a clean checkout and from an Editor previously used for another profile.
2. Sequential builds for two different profiles do not leak settings into each other.
3. Each Android artifact contains the expected manifest activity and runtime metadata.

## 8. Phase D: workflow matrix expansion

1. Add explicit matrix properties rather than overloading a display-name field:

   1. `runtime`.
   2. `targetPlatform`.
   3. `cache`.
   4. `androidTargetSdkVersion`.
   5. `androidExportType`.
   6. `development`.
   7. `artifactName`.
   8. `publishTarget` where applicable.

2. Pass the runtime and development flavor through `customParameters`.
3. Set `buildMethod: BuildOpenBlocks.CommandLine`.
4. Replace the generic Android OpenXR Google Play AAB entry with Android XR as the sole Google Play AAB producer. Retain the generic Android OpenXR APK as a separate cross-device sideload artifact.
5. Add Meta Quest as a new entry with its own artifact name and SDK selection.
6. Update every artifact consumer when names change:

   1. Release packaging.
   2. itch.io publication.
   3. Google Play publication.
   4. Meta publication.
   5. Any Steam Frame or device-specific packaging.

7. Add iOS/Zapbox only after its discovery gate is complete.
8. Use `max-parallel` only if licensing, cache pressure, or runner capacity requires it.

Acceptance criteria:

1. Every matrix entry has a unique artifact name.
2. Every publication job downloads the artifact produced by its intended entry.
3. Release and development artifacts cannot overwrite one another.
4. A missing or duplicate artifact causes an explicit failure.

## 9. Validation sequence

Run validation in this order to keep failures attributable:

1. Static checks:

   1. YAML parse and workflow lint.
   2. C# formatting and repository checks.
   3. EditMode tests for argument and profile selection.

2. Existing-platform CI smoke tests:

   1. Linux 2D.
   2. Windows OpenXR.
   3. Existing Android OpenXR APK.

3. Cache tests:

   1. Clean miss.
   2. Main-eligible save.
   3. Restore hit.
   4. Dependency-change miss or safe reimport.

4. New-runtime CI tests:

   1. Android XR AAB.
   2. Meta Quest APK.
   3. Generic Android OpenXR APK after all profiles exist.

5. Artifact inspection:

   1. File type and expected filename.
   2. Package/application ID.
   3. Version code and version name.
   4. Minimum and target SDK.
   5. ABI contains ARM64 and no unintended ABI.
   6. Android activity type and resizable property.
   7. OpenXR/Android XR/Meta manifest metadata.
   8. Presence of symbols where requested.

6. Hardware smoke tests:

   1. Launch generic OpenXR APK on at least one Quest-class device and one other supported OpenXR device when available.
   2. Launch the Meta-specific APK on Quest.
   3. Launch the Android XR build on Android XR hardware or the approved simulator path.
   4. Verify controllers/hands, rendering, save/load, menus, and application resume.
   5. Exercise passthrough, anchors, or composition layers only when included in that runtime profile.

7. Publication dry runs:

   1. Verify store tooling accepts each artifact without publishing it.
   2. Confirm Google Play recognizes the Android XR AAB and API level.
   3. Confirm Meta tooling accepts the Quest APK and API level.

## 10. Logging and diagnostics

1. Prefix all new build-method logs with `[OBXRBUILD]`.
2. Log the resolved runtime, target, output, SDK, export type, development flag, loader, and enabled required features.
3. Never log secrets or the full process environment.
4. Emit a final bounded settings summary immediately before `BuildPipeline.BuildPlayer`.
5. Emit restoration success or failure for each temporary settings scope.
6. When checking Unity Editor logs locally, search the entire relevant log for `[OBXRBUILD]` and compare timestamps with the current clock.
7. Store large CI or Unity logs as artifacts and report only targeted error lines in reviews.

## 11. Proposed implementation commit sequence

The plan file is excluded from this sequence and must remain uncommitted.

1. `Update CI to Unity 6.6 and unity-builder v5`
2. `Restrict Unity Library caches to safe Unity 6 data`
3. `Pass Android target SDK through unity-builder`
4. `Remove unused BuildTiltBrush workflow options`
5. `Add Open Blocks command-line build entry point`
6. `Add temporary XR loader and feature configuration`
7. `Add deterministic Android XR build profile`
8. `Add separate Meta Quest build profile`
9. `Expand CI matrix for Android XR and Meta Quest`
10. `Update artifact packaging and publication routing`
11. `Add Android artifact validation checks`
12. `Add iOS Zapbox support` only if its discovery gate is approved

Each commit should compile or validate independently. Do not combine cache changes, build-script introduction, and platform expansion into one commit.

## 12. Risks and mitigations

1. Risk: Unity 6 cache restore causes nondeterministic build failures.
   Mitigation: whitelist only proven cache databases, save only from main, and test a restore hit.
2. Risk: settings from one matrix runtime leak into another.
   Mitigation: disposable settings scopes plus before/after snapshots and sequential-profile tests.
3. Risk: Android XR features are enabled by duplicated feature ID rather than intended type.
   Mitigation: select required features by concrete C# type.
4. Risk: generic Android OpenXR becomes vendor-specific.
   Mitigation: separate runtime profiles and validate extensions across devices.
5. Risk: publication jobs silently consume renamed or wrong artifacts.
   Mitigation: explicit artifact names, exact download paths, and one-artifact assertions.
6. Risk: per-job package removal invalidates caches or modifies `packages-lock.json`.
   Mitigation: prefer installed-but-disabled XR packages; otherwise key caches by package set.
7. Risk: build failure leaves the checkout or Editor configured for the wrong runtime.
   Mitigation: restoration in `finally`, dirty-tree checks, and forced-failure tests.
8. Risk: Open Brush product assumptions enter Open Blocks.
   Mitigation: port mechanisms only; independently specify features, identifiers, branding, scenes, and outputs.
9. Risk: store SDK policies change.
   Mitigation: keep API levels in the matrix and verify them from the generated artifact.
10. Risk: adding iOS/Zapbox expands scope into signing and proprietary dependencies.
    Mitigation: keep it behind an explicit discovery and approval gate.

## 13. Completion criteria

The work is complete when all of these statements are true:

1. Existing CI targets build with Unity `6000.6.0f1` through `unity-builder@v5`.
2. Unity 6 Library caches restore without causing known stale-cache failures.
3. The workflow contains no unused BTB-derived matrix parameters.
4. Android SDK levels are set through supported build inputs or the Open Blocks build method, not serialized-file edits.
5. Release and development builds apply their intended `BuildOptions`.
6. Generic Android OpenXR, Android XR, and Meta Quest are explicit and independently configured artifacts.
7. Build settings are restored after both successful and failed builds.
8. Artifact inspection confirms SDK, ABI, manifest, package, version, and output type.
9. Available target hardware or approved simulation validates launch and core interaction.
10. Publication jobs consume the correct artifacts and pass non-publishing store validation.
11. iOS/Zapbox is either implemented after approval or explicitly recorded as deferred.

## 14. Out of scope unless separately requested

1. Copying Open Brush branding, application IDs, scenes, versioning rules, or release naming.
2. Copying Open Brush Viewer, Photon, Steam, Pimax, Rift, or product-specific upload behavior.
3. Publishing artifacts to any store.
4. Changing device feature behavior without a corresponding Open Blocks requirement.
5. Committing or pushing this planning document.
