# TODO: Format Parsing Bugs

- [ ] Handle `GLB` in `AssetsServiceClient.ParseAsset(...)`.
  The parser switch handles `GLTF`, `GLTF1`, and `GLTF2`, but not `GLB`, so valid binary glTF entries from the API are dropped entirely instead of being mapped into `entryAssets.gltf_package`.
  Reference: `Assets/Scripts/api_clients/assets_service_client/AssetsServiceClient.cs`

- [ ] Fix glTF slot selection so an earlier `GLTF1` does not block a later valid `GLTF2`.
  The parser currently keeps only the first non-GLB glTF entry via `entryAssets.gltf = entryAssets.gltf ?? gltfAssets;`.
  This allows an unsupported `GLTF1` entry to occupy the slot and discard a later `GLTF2` entry that should actually be loadable.
  Reference: `Assets/Scripts/api_clients/assets_service_client/AssetsServiceClient.cs`

- [ ] Make zero-attempt loader diagnostics explicit about parse loss versus API loss.
  The current `no valid download formats` log is based on the reduced `ObjectStoreEntry.assets` structure, not the raw API `formats[]` array.
  That means the log can imply the API returned nothing usable when the real problem is that `ParseAsset(...)` discarded usable formats first.
  Reference: `Assets/Scripts/api_clients/objectstore_client/ObjectStoreClient.cs`

- [ ] Remove or reframe the stale `GLTF1` rejection inside `AttemptGltfAsset(...)`.
  `TryGetRawFileDataWithFallback(...)` already filters out `GLTF1` before scheduling attempts, so this branch is not part of the normal attempted-download path.
  Leaving it as a failure reason risks conflating `skipped as unsupported` with `attempted and failed`.
  Reference: `Assets/Scripts/api_clients/objectstore_client/ObjectStoreClient.cs`
