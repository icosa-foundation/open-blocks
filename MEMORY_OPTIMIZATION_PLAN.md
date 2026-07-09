# Memory & Performance Optimization Plan

Status: proposal / working document.
Scope: reducing RAM usage (mobile OOM crashes) and improving runtime performance, with a focus on
`MMesh` and related structures. This document assumes the changes on the
`claude/mobile-memory-optimization-9tmfpz` branch have landed; the "Completed" section summarizes
them for context, and everything after it is recommended future work, ranked by expected
impact-per-risk.

---

## Completed on this branch (context)

- **Undo/redo snapshots stored as serialized bytes.** `AddMeshCommand` retains the mesh in the
  compact `.blocks` wire format instead of a live MMesh object graph (~10-20x smaller). The undo
  stack was the largest unbounded memory consumer in long editing sessions.
- **`MMesh.Clone()` no longer copies derived data.** The reverse table (a `HashSet<int>` per
  vertex) and per-face position/normal/color caches are lazily rebuilt on first use. Clones happen
  on every add command, spatial-index update, grab, and menu preview.
- **Poly menu creations retain only raw `.blocks` bytes.** Previously each creation permanently
  held two parsed mesh lists plus the `PeltzerFile` (several full scenes of parsed geometry across
  the menu tabs). Details/open/import now re-parse on demand.
- **ReMesher buffers proportional to content.** Removed the 32MB static fill-buffer cache; MeshInfo
  vertex arrays grow geometrically instead of preallocating ~1.6MB per material bucket; Unity
  meshes upload only the vertices in use rather than full 32k-element arrays.
- Assorted fixes: preview-template mesh leak in `MeshRepresentationCache.InvalidatePreviews`,
  per-frame `mesh.vertices` array copies, clone `localBounds` loss (which had silently disabled
  SpatialIndex point-in-mesh queries and MeshValidator's relative-volume check for clones),
  per-mesh `System.Random` allocation, reverse-table HashSet aliasing in `CloneWithNewIdAndGroup`.

---

## 0. Measure before optimizing further

Everything below should be gated on device profiling, both to rank the remaining work and to
verify the landed changes behave as predicted.

- Capture **Unity Memory Profiler snapshots on-device** (Quest/Android) at four points: fresh
  start, after browsing all Poly menu tabs, after loading the largest available model, and after a
  30-minute scripted editing session. Diff the snapshots; rank by retained size.
- Add a **debug-console command that dumps live counts**: model mesh count and total verts/faces,
  undo/redo stack entry count and total serialized bytes, `MeshRepresentationCache` entry counts,
  ReMesher MeshInfo count and summed buffer capacities, Poly menu creation count and summed
  `rawFileData` bytes. This is cheap and makes regressions visible without a profiler.
- Watch **`Profiler.GetMonoUsedSizeLong` vs. total app memory** — if native (GPU mesh/texture)
  memory dominates after these changes, prioritize items 4 and 6 below over managed-heap work.
- Specifically verify on device: undo/redo latency while holding the undo button on a large scene
  (the snapshot change trades a near-free apply for a deserialize per step; expected low
  single-digit ms per typical mesh, but confirm).

---

## 1. `Vertex` class → struct (highest managed-memory impact, needs compiler-verified refactor)

`Vertex` is an immutable class (`int id`, `Vector3 loc`) stored in `Dictionary<int, Vertex>` in
every MMesh. Per vertex that costs an object header + dictionary reference (~56-64 bytes) for 16
bytes of payload, plus one GC-tracked object. Converting to a `readonly struct` roughly **halves
per-vertex memory in every mesh and every retained copy** and removes tens of thousands of heap
objects per scene, which also shortens GC pauses.

Why it wasn't done on this branch: ~750 usages, 30+ collections of `Vertex`, at least one `== null`
check (`ExtrusionOperation.cs:549`), and class reference-equality semantics anywhere a
`HashSet<Vertex>`/`List<Vertex>.Contains` is used today. A struct silently switches those to
field-wise equality. This needs a compile-run-test loop, done as a single mechanical PR:

1. Make `Vertex` a `readonly struct`; delete null checks (use `TryGetValue` patterns instead).
2. Audit every `HashSet<Vertex>` / `Contains` / `Remove` site for equality-semantics changes
   (field-wise equality is *usually* what the code actually wants, but verify each).
3. Run the editor test suite plus a scripted editing session; profile before/after.

The same treatment applies to `Face` in principle but Face is genuinely mutable
(`SetProperties`, cached lists) and referenced widely; see item 2 for a cheaper alternative.

## 2. Compact `Face` storage

Per face today: a `ReadOnlyCollection<int>` wrapper around a `List<int>` around an `int[]`
(3 objects for one array), three cache `List`s, and two `List<Triangle>` triangulations. For a
5k-face mesh that's ~40k heap objects before any cloning.

- Replace `ReadOnlyCollection<int> vertexIds` with a plain `int[]` exposed as
  `IReadOnlyList<int>`/indexer (vertexIds are never mutated after construction — faces are
  replaced, not edited). Saves 2 objects + ~48 bytes per face and speeds iteration.
- Store triangulations as `Triangle[]` (or a single `int[]`) instead of `List<Triangle>`; they are
  computed once and never appended to afterwards.
- Consider dropping `cachedMeshSpacePositions`/`cachedRenderNormals`/`cachedColors` entirely and
  having `MeshHelper` read positions via `mesh.VertexPositionInMeshCoords` into shared scratch
  buffers. The caches exist to speed re-render of unchanged faces, but the ReMesher already caches
  the assembled `MeshGenContext` per mesh in `MeshRepresentationCache`, so the per-face caches are
  a third copy of the same data. Needs profiling to confirm the re-render path doesn't regress.

## 3. Budget the undo stack in bytes, not entries

`Model.undoStackMaxSize` is 80 *entries*, but one entry can be a composite holding snapshots of a
whole multi-selection. Now that `AddMeshCommand` snapshots are `byte[]`, sizing is trivial:

- Track a running total of serialized bytes on the undo stack; when it exceeds a budget (e.g.
  32MB on mobile, 128MB desktop), drop the oldest entries regardless of count. This converts the
  worst remaining unbounded consumer into a hard cap with graceful degradation (shorter history
  for huge scenes instead of a crash).
- Optional latency insurance: keep the top 1-2 entries' meshes pre-deserialized (or deserialize
  the next entry on the background thread after each undo) if device profiling shows rapid
  repeated undo hitching. Don't build this until measured.

## 4. ReMesher GPU-side: 16-bit indices and leaner vertex streams

`MAX_VERTS_PER_MESH` is 32768, which fits `UInt16` exactly:

- Set `mesh.indexFormat = IndexFormat.UInt16` on MeshInfo meshes — halves index-buffer memory and
  upload bandwidth for every batched mesh. (Verify the two sentinel verts keep total count ≤ 65535
  — they do.)
- The per-vertex transform index is uploaded as a `Vector2` UV channel (8 bytes/vertex) to carry a
  small integer. Moving it into, e.g., the unused alpha of `colors32` or a single-component UV
  (`SetUVs` with `Vector2` is the current API; a custom `VertexAttributeDescriptor` layout via
  `SetVertexBufferData` could cut it to 1-4 bytes) saves 4-7 bytes/vertex CPU+GPU and shrinks
  every re-upload. This pairs well with a shader-side change reading the index from the new
  location. Medium effort, purely mechanical risk.
- Longer term, `Mesh.SetVertexBufferData` with a packed interleaved layout would eliminate the
  four separate managed arrays entirely, but only pursue if profiling shows this path still
  matters after the above.

## 5. SpatialIndex: second copy of all face geometry

`FaceInfo.border` is a `List<Vector3>` of every face's model-space corner positions, retained for
every face in the scene — effectively a full extra copy of scene geometry, plus octree node
overhead, plus `EdgeInfo` per edge. It powers hover/selection queries, so it can't just go away,
but:

- Store `border` as `Vector3[]` (or indices into a per-mesh position array) instead of `List`.
- Profile whether `EdgeInfo`/`FaceInfo` dictionaries or the octree nodes dominate; octree
  implementations often benefit from node pooling and flat storage.
- The index is rebuilt from a full `mesh.Clone()` on every mesh change. Since `AddToIndex` only
  reads positions/faces, a cheaper "geometry snapshot" type (positions array + face vertex-id
  arrays) would avoid cloning Face objects at all. Moderate effort; the clone is already much
  cheaper after this branch, so measure first.

## 6. GPU/native memory audit (textures, thumbnails, render targets)

Managed-heap fixes don't help if native memory is the killer on device. Cheap, low-risk wins to
audit:

- **Poly menu thumbnails**: `ProcessGetThumbnailTexture` creates 512x384 RGBA32 textures with a
  full mip chain and keeps them CPU-readable (`new Texture2D(512, 384)`, `ReadPixels`, `Apply()`).
  `Apply(false, true)` (no mips, non-readable) cuts each thumbnail roughly 4x (~2MB → ~0.75MB);
  across a few hundred menu tiles this is hundreds of MB of headroom. Same for the local-file path
  (`LoadImage(bytes, markNonReadable: true)`, no-mip constructor).
- Verify Android texture import settings use ASTC and that `GifRecorder`'s
  `capturedGifFrames` (`List<Color32[]>`, ~1MB per 512x512 frame, unbounded while recording) is
  either disabled on mobile or frame-capped.
- Check for leaked `RenderTexture`s / preview cameras after save-thumbnail generation.

## 7. `MeshRepresentationCache` lifetime discipline

The cache holds, per touched mesh: mesh-space `MeshGenContext`s (feeds ReMesher — necessary),
model-space `MeshGenContext`s, and preview-template GameObjects with their own Unity meshes. The
latter two grow with hover/selection history and are only evicted when a mesh changes or is
deleted.

- Do **not** add naive LRU eviction: `GeneratePreview` clones templates with `isPreview = true`,
  meaning clones share the template's Unity meshes; destroying an evicted template would break
  live previews. Any eviction needs shared-mesh ownership (refcount or copy-on-evict) first.
- Cheaper first step: clear the model-space cache and preview templates on scene clear/load and on
  `Application.lowMemory` (see item 8), where no previews can be live.

## 8. `Application.lowMemory` pressure valve

Android delivers a low-memory callback before killing the app. Wire it to: trim the oldest half of
the undo stack, `MeshRepresentationCache.Clear()` (safe when no grab in progress), drop
non-visible Poly menu pages' preview GameObjects, and `Resources.UnloadUnusedAssets()`. This is a
day of work and converts many would-be OOM kills into a logged, recoverable event. Log each firing
to analytics so the remaining pressure sources are visible in the field.

## 9. Load-time transient spikes

Opening/importing a model currently materializes: raw bytes + parsed `PeltzerFile` (all meshes) +
per-mesh ReMesher contexts + Unity meshes, all within a frame or two. For large models the *peak*
is what kills the app even if steady-state fits.

- Stream model load: add meshes to the model over several frames (the ReMesher already defers via
  `meshesPendingAdd`; the parse loop in `PeltzerFileHandler`/`LoadPeltzerFileIntoModel` could
  yield between meshes).
- After load, the raw download bytes and intermediate structures should be released promptly
  (verify nothing pins them via closures — the async request callbacks are a historical source of
  accidental retention).

## 10. Smaller follow-ups (grab-bag, each < half a day)

- `MeshHelper.UpdateMeshes` still allocates a `Dictionary` + per-material `List`s per call in the
  drag-resize loop; a reusable map keyed the same way would zero out the remaining churn there.
- `Model.GetMeshes(IEnumerable<int>)` uses LINQ `Select` inside a `List` ctor; called in some tool
  paths — replace with a plain loop (matches `GetMatchingMeshes`).
- `MMesh.GenerateFaceId/GenerateVertexId/GenerateMeshId` use the shared static `System.Random`
  from background threads in some flows (CSG); `System.Random` corrupts silently under races.
  Move to the `[ThreadStatic]` instance introduced for jitter.
- `ObjectStoreClient`/`AssetsServiceClient` download buffers: confirm `DownloadHandlerBuffer`
  results aren't retained past parse (menu holds `rawFileData` deliberately now; nothing else
  should).
- Audit `Debug.Log` string interpolation in per-frame paths (allocation + logcat cost on device).

---

## Suggested sequencing

| Order | Item | Impact | Risk | Effort |
|-------|------|--------|------|--------|
| 1 | §0 device profiling + debug memory dump | enables everything | none | S |
| 2 | §8 lowMemory pressure valve | crash → degrade | low | S |
| 3 | §3 byte-budgeted undo stack | caps last unbounded consumer | low | S |
| 4 | §6 thumbnail/native audit | large on device | low | S |
| 5 | §4 16-bit indices (+ transform channel) | GPU+CPU per scene | low-med | M |
| 6 | §1 Vertex struct | halves per-vertex managed memory | med (mechanical) | M |
| 7 | §2 Face compaction | large object-count cut | med | M |
| 8 | §5 SpatialIndex snapshotting | second geometry copy | med | M-L |
| 9 | §9 streamed loads | peak-memory spikes | med | M-L |

Items 1-4 are safe to do immediately and would likely be enough to stop the crashes outright;
items 5-9 are the structural payoff work and should each land as an isolated, profiled PR.
