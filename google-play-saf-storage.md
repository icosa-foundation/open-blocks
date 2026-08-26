# Google Play SAF Model Storage

## Scope

Google Play Android builds use the Android Storage Access Framework (SAF) for
canonical, user-visible model saves. Other platforms and Android distributions
retain the existing directory-backed implementation.

Enable the Google Play backend with the `OPEN_BLOCKS_GOOGLE_PLAY` scripting
define for the Android build target.

The build manifest processor removes `MANAGE_EXTERNAL_STORAGE` and
`requestLegacyExternalStorage` from generated Google Play manifests. Those
settings remain available to existing non-Google-Play Android builds.

## Architecture

Open Blocks differs from Open Brush in two important ways:

- A save is a small directory of related files rather than one archive.
- The existing serialization and menu-loading paths already exchange model
  data as byte arrays.

For those reasons, Open Blocks uses a byte-oriented model-directory backend
instead of wrapping SAF file descriptors in `FileStream`.

`IUserModelStorageBackend` is the boundary for user-visible models:

- list models using opaque backend identities;
- read one named child document;
- save or replace one complete model directory;
- delete one model;
- request shared-folder access when required.

`LocalUserModelStorageBackend` preserves the previous filesystem behavior.
`SafUserModelStorageBackend` delegates provider operations to
`OpenBlocksSafBridge`.

Gameplay, menu, serialization, and loading code do not inspect `content://`
URIs or invoke Android APIs directly.

## Storage Ownership

For Google Play builds:

- The selected `Blocks` or `Open Blocks` SAF tree is the only canonical,
  user-visible store.
- Canonical models live below its `OfflineModels` child.
- `Application.persistentDataPath/OpenBlocksLocalOnly` contains configuration,
  autosaves, and serialization/export working files.
- `Application.persistentDataPath/OpenBlocksSafRecovery/<root-id>/journals`
  contains incomplete SAF transaction state.

App-private working files are not enumerated as saved models and are not
reconciled with SAF. Google Play builds do not create the legacy app-private
manual-save copy in addition to the SAF model. Autosaves remain app-private
and are not deleted when a shared save fails.

## Catalog and Reads

The Java bridge enumerates `OfflineModels` with one provider query. Catalog
records contain:

- the opaque document URI used as identity;
- the display name used by the menu;
- provider last-modified metadata.

Reserved transaction and backup directories are excluded.

Provider catalog queries, model reads, thumbnail reads, saves, and deletions
run on the existing serialized Poly Menu background-work queue rather than the
Unity main thread. A failed catalog query retains the last successful
snapshot; failure is not interpreted as an empty folder. The catalog is
refreshed when the app resumes so external document changes can be observed,
and identical snapshots do not rebuild menu entries.

The menu reads only `thumbnail.png` when it needs a thumbnail and only
`model.blocks` when it needs model geometry. No model directory is mirrored or
materialized.

## Save Transaction

All child documents are written to a uniquely named temporary model directory.
The required `model.blocks`, `model.obj`, and `materials.mtl` documents must
complete before publication. Optional thumbnail and export documents are added
when present.

New saves:

1. Create and journal a temporary directory.
2. Write and close every model document.
3. Journal that the temporary payload is complete.
4. Journal replacement installation intent.
5. Rename the temporary directory to its canonical display name.
6. Journal the installed identity.
7. Remove the journal.

Overwrites:

1. Complete the same temporary payload.
2. Journal backup intent.
3. Rename the old destination to a reserved backup name.
4. Journal the returned backup identity.
5. Journal replacement installation intent.
6. Rename the temporary directory to the canonical display name.
7. Journal the returned canonical identity.
8. Delete the backup.
9. Remove the journal.

The existing canonical model is never opened in truncating mode. Provider
renames may return different identities, so each returned URI replaces the
previous identity. The current model retains both that opaque identity and its
display name; an overwrite does not re-enumerate the provider on the Unity main
thread to recover the name.

Transactions affecting saved models are serialized by the existing Poly Menu
background-work queue.

## Recovery

Journal records are versioned, identify their transaction kind and selected
root, and retain their original creation time. Updates use a same-filesystem,
flushed temporary file followed by an atomic replacement. Journals are
namespaced by a hash of the selected SAF root, and root identity is validated
again while reading them, so recovery from one selected tree cannot be applied
to another.

Startup recovery handles interruption:

- while writing a temporary payload;
- before or after backing up an old destination;
- before or after installing the replacement;
- while deleting the backup.

Recovery writes the same pre-mutation and post-mutation states as a live save,
including explicit installation and rollback states. This keeps another
process interruption during recovery deterministic. Recovery restores the
previous model or completes the replacement based on the last durable state.
It does not delete the only known canonical copy. Malformed, wrong-root, and
unknown-version journal records are retained and reported as recovery
failures.

## Folder Selection

The folder picker is launched only when a local save requires shared storage
and no valid persisted grant exists. The user must select a folder named
`Blocks` or `Open Blocks`; invalid selections are rejected without replacing a
previous valid selection.

Cancelling selection leaves the app running. The attempted shared save is
reported as failed, while the in-memory model and app-private autosaves remain
available.

## Validation

Automated local-backend tests cover:

- save, list, read, overwrite, and delete;
- opaque identity preservation;
- path traversal rejection.

An editor test verifies that Google Play manifest processing removes broad
storage access while preserving unrelated permissions.

The Java bridge is compiled against Android API 34 as a source check.

Before release, run on the target Google Play Android device:

1. Select both valid folder names and reject an invalid folder.
2. Save a new model, reopen it, and verify its thumbnail.
3. Overwrite repeatedly and confirm the document identity update is honored.
4. Delete a model.
5. Cancel selection and confirm no successful save is reported.
6. Revoke and restore the persisted permission.
7. Kill the process at every journal state and verify recovery.
8. Fill storage during each child-document write.
9. Add and remove model directories externally, then relaunch.
10. Confirm the generated manifest contains no broad external-storage
    permission.

Current implementation deliberately targets the local Android Documents
provider. Cloud-backed providers need separate latency, capability, and
interruption testing before being treated as supported.
