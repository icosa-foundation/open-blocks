package foundation.icosa.openblocks.storage;

import android.app.Activity;
import android.app.Fragment;
import android.content.ContentResolver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.UUID;

/**
 * Narrow Android SAF bridge for Open Blocks' canonical user-visible models.
 *
 * The selected tree is canonical. This class never mirrors it into app-private
 * storage. App-private files contain transaction journals only.
 */
public final class OpenBlocksSafBridge {
    private static final String LOG_PREFIX = "OB_SAF_BRIDGE";
    private static final String PREFS_NAME = "open_blocks_saf";
    private static final String PREF_TREE_URI = "tree_uri";
    private static final String MODELS_DIRECTORY = "OfflineModels";
    private static final String MODEL_FILE_NAME = "model.blocks";
    private static final String PICKER_FRAGMENT_TAG = "OpenBlocksSafPicker";
    private static final int PICK_TREE_REQUEST = 17241;
    private static final String TEMP_PREFIX = ".__openblocks_tx_";
    private static final String BACKUP_PREFIX = ".__openblocks_backup_";

    // Values intentionally match StorageAccessRequestState in C#.
    private static final int ACCESS_IDLE = 1;
    private static final int ACCESS_PENDING = 2;
    private static final int ACCESS_GRANTED = 3;
    private static final int ACCESS_CANCELLED = 4;
    private static final int ACCESS_FAILED = 5;

    private static volatile int accessRequestState = ACCESS_IDLE;

    private OpenBlocksSafBridge() {
    }

    public static boolean isReady(Activity activity) {
        try {
            Uri treeUri = getPersistedTreeUri(activity);
            return treeUri != null && hasPersistedPermission(activity, treeUri);
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Readiness check failed", exception);
            return false;
        }
    }

    public static String getRootIdentity(Activity activity) {
        Uri treeUri = getPersistedTreeUri(activity);
        return treeUri != null && hasPersistedPermission(activity, treeUri)
            ? treeUri.toString()
            : null;
    }

    public static int getAccessRequestState(Activity activity) {
        return isReady(activity) ? ACCESS_GRANTED : accessRequestState;
    }

    public static boolean requestAccess(Activity activity) {
        if (isReady(activity)) {
            accessRequestState = ACCESS_GRANTED;
            return true;
        }

        if (accessRequestState == ACCESS_PENDING) {
            return true;
        }

        try {
            accessRequestState = ACCESS_PENDING;
            activity.runOnUiThread(() -> {
                try {
                    Fragment existing = activity.getFragmentManager().findFragmentByTag(PICKER_FRAGMENT_TAG);
                    if (existing == null) {
                        activity.getFragmentManager()
                            .beginTransaction()
                            .add(new PickerFragment(), PICKER_FRAGMENT_TAG)
                            .commitAllowingStateLoss();
                    }
                } catch (Exception exception) {
                    accessRequestState = ACCESS_FAILED;
                    android.util.Log.w(LOG_PREFIX, "Could not launch folder picker", exception);
                }
            });
            return true;
        } catch (Exception exception) {
            accessRequestState = ACCESS_FAILED;
            android.util.Log.w(LOG_PREFIX, "Folder access request failed", exception);
            return false;
        }
    }

    public static String listModels(Activity activity) {
        try {
            Uri modelsUri = requireModelsDirectory(activity);
            JSONArray models = new JSONArray();
            for (DocumentRecord child : queryChildren(activity, modelsUri)) {
                if (!child.isDirectory || isReservedName(child.displayName)) {
                    continue;
                }
                if (!hasModelFile(activity, child.uri)) {
                    continue;
                }
                JSONObject model = new JSONObject();
                model.put("id", child.uri.toString());
                model.put("displayName", child.displayName);
                model.put("lastModified", child.lastModified);
                models.put(model);
            }

            JSONObject result = new JSONObject();
            result.put("models", models);
            return result.toString();
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Model listing failed", exception);
            return null;
        }
    }

    public static byte[] readModelFile(Activity activity, String modelId, String fileName) {
        if (!isSafeLeafName(fileName)) {
            return null;
        }

        try {
            Uri modelUri = Uri.parse(modelId);
            Uri fileUri = findChild(activity, modelUri, fileName);
            if (fileUri == null) {
                return null;
            }

            try (InputStream input = activity.getContentResolver().openInputStream(fileUri);
                 ByteArrayOutputStream output = new ByteArrayOutputStream()) {
                if (input == null) {
                    return null;
                }
                byte[] buffer = new byte[64 * 1024];
                int count;
                while ((count = input.read(buffer)) >= 0) {
                    output.write(buffer, 0, count);
                }
                return output.toByteArray();
            }
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Model document read failed", exception);
            return null;
        }
    }

    public static String beginModelWrite(Activity activity, String transactionId) {
        try {
            Uri modelsUri = requireModelsDirectory(activity);
            Uri temporaryUri = DocumentsContract.createDocument(
                activity.getContentResolver(),
                modelsUri,
                DocumentsContract.Document.MIME_TYPE_DIR,
                TEMP_PREFIX + transactionId);
            if (temporaryUri == null) {
                return null;
            }
            if (!writeJournal(activity, transactionId, "WritingTemporary",
                temporaryUri.toString(), null, null, null)) {
                deleteDocument(activity, temporaryUri.toString());
                return null;
            }
            return temporaryUri.toString();
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Temporary model directory creation failed", exception);
            return null;
        }
    }

    public static boolean writeModelFile(
        Activity activity,
        String directoryId,
        String fileName,
        String mimeType,
        byte[] bytes) {
        if (!isSafeLeafName(fileName) || bytes == null) {
            return false;
        }

        try {
            Uri directoryUri = Uri.parse(directoryId);
            Uri existing = findChild(activity, directoryUri, fileName);
            if (existing != null && !DocumentsContract.deleteDocument(activity.getContentResolver(), existing)) {
                return false;
            }

            Uri fileUri = DocumentsContract.createDocument(
                activity.getContentResolver(), directoryUri, mimeType, fileName);
            if (fileUri == null) {
                return false;
            }

            try (OutputStream output = activity.getContentResolver().openOutputStream(fileUri, "rwt")) {
                if (output == null) {
                    return false;
                }
                output.write(bytes);
                output.flush();
            }
            return true;
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Model document write failed: " + fileName, exception);
            return false;
        }
    }

    public static boolean recordTemporaryComplete(
        Activity activity,
        String transactionId,
        String temporaryId,
        String destinationId,
        String displayName) {
        return writeJournal(activity, transactionId, "TemporaryComplete",
            temporaryId, destinationId, null, displayName);
    }

    public static String backupDestination(Activity activity, String transactionId, String destinationId) {
        try {
            Uri backup = DocumentsContract.renameDocument(
                activity.getContentResolver(),
                Uri.parse(destinationId),
                BACKUP_PREFIX + transactionId);
            return backup == null ? null : backup.toString();
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Destination backup failed", exception);
            return null;
        }
    }

    public static boolean recordBackingUpOriginal(
        Activity activity,
        String transactionId,
        String temporaryId,
        String destinationId,
        String displayName) {
        return writeJournal(activity, transactionId, "BackingUpOriginal",
            temporaryId, destinationId, null, displayName);
    }

    public static boolean recordOriginalBackedUp(
        Activity activity,
        String transactionId,
        String temporaryId,
        String backupId,
        String displayName) {
        return writeJournal(activity, transactionId, "OriginalBackedUp",
            temporaryId, null, backupId, displayName);
    }

    public static String installReplacement(Activity activity, String temporaryId, String displayName) {
        if (!isSafeLeafName(displayName)) {
            return null;
        }
        try {
            Uri installed = DocumentsContract.renameDocument(
                activity.getContentResolver(), Uri.parse(temporaryId), displayName);
            return installed == null ? null : installed.toString();
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Replacement installation failed", exception);
            return null;
        }
    }

    public static boolean recordInstallingReplacement(
        Activity activity,
        String transactionId,
        String temporaryId,
        String backupId,
        String displayName) {
        return writeJournal(activity, transactionId, "InstallingReplacement",
            temporaryId, null, backupId, displayName);
    }

    public static boolean recordReplacementInstalled(
        Activity activity,
        String transactionId,
        String installedId,
        String backupId,
        String displayName) {
        return writeJournal(activity, transactionId, "ReplacementInstalled",
            null, installedId, backupId, displayName);
    }

    public static boolean completeTransaction(Activity activity, String transactionId) {
        File journal = getJournalFile(activity, transactionId);
        return !journal.exists() || journal.delete();
    }

    public static boolean abandonModelWrite(
        Activity activity,
        String transactionId,
        String temporaryId) {
        return deleteDocument(activity, temporaryId) &&
            completeTransaction(activity, transactionId);
    }

    public static boolean deleteDocument(Activity activity, String documentId) {
        if (documentId == null || documentId.isEmpty()) {
            return true;
        }
        try {
            Uri uri = Uri.parse(documentId);
            return !documentExists(activity, uri) ||
                DocumentsContract.deleteDocument(activity.getContentResolver(), uri);
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Document deletion failed", exception);
            return false;
        }
    }

    public static boolean recoverIncompleteTransactions(Activity activity) {
        if (!isReady(activity)) {
            return false;
        }

        Uri treeUri = getPersistedTreeUri(activity);
        String expectedRootId = treeUri == null ? null : getRootIdentity(treeUri);
        File[] journals = getJournalDirectory(activity).listFiles(
            (directory, name) -> name.endsWith(".json"));
        if (journals == null) {
            return true;
        }

        boolean allRecovered = true;
        for (File journal : journals) {
            try {
                JSONObject record = new JSONObject(
                    new String(Files.readAllBytes(journal.toPath()), StandardCharsets.UTF_8));
                String transactionId = record.optString("transactionId");
                if (record.optInt("version", -1) != 1 ||
                    !"model-directory-replacement".equals(record.optString("kind")) ||
                    !expectedRootId.equals(record.optString("rootId")) ||
                    transactionId.isEmpty() ||
                    !journal.getName().equals(transactionId + ".json")) {
                    allRecovered = false;
                    android.util.Log.w(
                        LOG_PREFIX,
                        "Retaining unrecognized transaction journal " + journal.getName());
                    continue;
                }

                String state = record.optString("state");
                String temporaryId = nullableString(record, "temporaryId");
                String installedId = nullableString(record, "destinationId");
                String backupId = nullableString(record, "backupId");
                String displayName = nullableString(record, "displayName");

                boolean recovered;
                if ("WritingTemporary".equals(state) || "TemporaryComplete".equals(state)) {
                    recovered = deleteDocument(activity, temporaryId);
                } else if ("BackingUpOriginal".equals(state)) {
                    Uri reservedBackup = findChild(
                        activity,
                        requireModelsDirectory(activity),
                        BACKUP_PREFIX + transactionId);
                    if (reservedBackup == null) {
                        // The namespace mutation had not started. The original remains canonical.
                        recovered = deleteDocument(activity, temporaryId);
                    } else {
                        // The old document may retain the same opaque ID across rename, so its URI
                        // is not evidence that it still has the canonical display name.
                        recovered = writeJournal(
                            activity,
                            transactionId,
                            "RollbackRequired",
                            temporaryId,
                            null,
                            reservedBackup.toString(),
                            displayName) &&
                            rollbackReplacement(
                                activity, temporaryId, reservedBackup.toString(), displayName);
                    }
                } else if ("OriginalBackedUp".equals(state)) {
                    if (!writeJournal(
                        activity,
                        transactionId,
                        "InstallingReplacement",
                        temporaryId,
                        null,
                        backupId,
                        displayName)) {
                        recovered = false;
                    } else {
                        recovered = installOrRollbackReplacement(
                            activity, transactionId, temporaryId, backupId, displayName);
                    }
                } else if ("InstallingReplacement".equals(state)) {
                    Uri canonical = findChild(
                        activity, requireModelsDirectory(activity), displayName);
                    if (canonical != null) {
                        recovered = recordInstalledAndCleanup(
                            activity,
                            transactionId,
                            canonical.toString(),
                            backupId,
                            displayName);
                    } else {
                        recovered = installOrRollbackReplacement(
                            activity, transactionId, temporaryId, backupId, displayName);
                    }
                } else if ("ReplacementInstalled".equals(state)) {
                    Uri installed = installedId == null ? null : Uri.parse(installedId);
                    if (installed != null && documentExists(activity, installed)) {
                        recovered = deleteDocument(activity, backupId);
                    } else {
                        Uri canonical = findChild(
                            activity, requireModelsDirectory(activity), displayName);
                        recovered = canonical != null &&
                            recordInstalledAndCleanup(
                                activity,
                                transactionId,
                                canonical.toString(),
                                backupId,
                                displayName);
                    }
                } else if ("RollbackRequired".equals(state)) {
                    recovered = rollbackReplacement(
                        activity, temporaryId, backupId, displayName);
                } else {
                    recovered = false;
                }

                if (recovered) {
                    Files.deleteIfExists(journal.toPath());
                } else {
                    allRecovered = false;
                }
            } catch (Exception exception) {
                allRecovered = false;
                android.util.Log.w(LOG_PREFIX, "Transaction recovery failed for " + journal.getName(), exception);
            }
        }
        return allRecovered;
    }

    private static boolean installOrRollbackReplacement(
        Activity activity,
        String transactionId,
        String temporaryId,
        String backupId,
        String displayName) throws Exception {
        String replacement = temporaryId == null
            ? null
            : installReplacement(activity, temporaryId, displayName);
        if (replacement != null) {
            return recordInstalledAndCleanup(
                activity, transactionId, replacement, backupId, displayName);
        }

        return writeJournal(
            activity,
            transactionId,
            "RollbackRequired",
            temporaryId,
            null,
            backupId,
            displayName) &&
            rollbackReplacement(activity, temporaryId, backupId, displayName);
    }

    private static boolean recordInstalledAndCleanup(
        Activity activity,
        String transactionId,
        String installedId,
        String backupId,
        String displayName) throws Exception {
        return writeJournal(
            activity,
            transactionId,
            "ReplacementInstalled",
            null,
            installedId,
            backupId,
            displayName) &&
            deleteDocument(activity, backupId);
    }

    private static boolean rollbackReplacement(
        Activity activity,
        String temporaryId,
        String backupId,
        String displayName) throws Exception {
        if (backupId == null) {
            return deleteDocument(activity, temporaryId);
        }

        Uri canonical = findChild(activity, requireModelsDirectory(activity), displayName);
        boolean canonicalAvailable = canonical != null;
        boolean backupAvailable = documentExists(activity, Uri.parse(backupId));

        if (backupAvailable && !canonicalAvailable) {
            canonicalAvailable = restoreBackup(activity, backupId, displayName);
        } else if (backupAvailable && canonicalAvailable &&
            !canonical.toString().equals(backupId)) {
            // Both copies still exist with different identities. Keep the journal and
            // both documents for a later deterministic recovery attempt.
            return false;
        }
        return canonicalAvailable && deleteDocument(activity, temporaryId);
    }

    private static boolean restoreBackup(Activity activity, String backupId, String displayName) {
        if (backupId == null || displayName == null) {
            return false;
        }
        try {
            Uri restored = DocumentsContract.renameDocument(
                activity.getContentResolver(), Uri.parse(backupId), displayName);
            return restored != null;
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Backup restoration failed", exception);
            return false;
        }
    }

    private static boolean writeJournal(
        Activity activity,
        String transactionId,
        String state,
        String temporaryId,
        String destinationId,
        String backupId,
        String displayName) {
        try {
            Uri treeUri = getPersistedTreeUri(activity);
            if (treeUri == null) {
                return false;
            }
            String rootId = getRootIdentity(treeUri);
            File journal = getJournalFile(activity, transactionId);
            long now = System.currentTimeMillis();
            long createdUtcMillis = now;
            if (journal.exists()) {
                JSONObject previous = new JSONObject(
                    new String(Files.readAllBytes(journal.toPath()), StandardCharsets.UTF_8));
                if (previous.optInt("version", -1) != 1 ||
                    !"model-directory-replacement".equals(previous.optString("kind")) ||
                    !rootId.equals(previous.optString("rootId")) ||
                    !transactionId.equals(previous.optString("transactionId"))) {
                    throw new IllegalStateException(
                        "Refusing to overwrite an unrecognized transaction journal.");
                }
                createdUtcMillis = previous.optLong("createdUtcMillis", now);
            }

            JSONObject record = new JSONObject();
            record.put("version", 1);
            record.put("kind", "model-directory-replacement");
            record.put("transactionId", transactionId);
            record.put("rootId", rootId);
            record.put("state", state);
            record.put("temporaryId", temporaryId == null ? JSONObject.NULL : temporaryId);
            record.put("destinationId", destinationId == null ? JSONObject.NULL : destinationId);
            record.put("backupId", backupId == null ? JSONObject.NULL : backupId);
            record.put("displayName", displayName == null ? JSONObject.NULL : displayName);
            record.put("createdUtcMillis", createdUtcMillis);
            record.put("updatedUtcMillis", now);

            File temporary = new File(journal.getParentFile(), journal.getName() + ".tmp");
            try (FileOutputStream output = new FileOutputStream(temporary)) {
                output.write(record.toString().getBytes(StandardCharsets.UTF_8));
                output.flush();
                output.getFD().sync();
            }
            Files.move(temporary.toPath(), journal.toPath(),
                StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
            return true;
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Transaction journal update failed", exception);
            return false;
        }
    }

    private static File getJournalDirectory(Activity activity) {
        Uri treeUri = getPersistedTreeUri(activity);
        if (treeUri == null) {
            throw new IllegalStateException("Cannot resolve recovery storage without a selected SAF root.");
        }
        File directory = new File(
            activity.getFilesDir(),
            "OpenBlocksSafRecovery/" + getRootIdentity(treeUri) + "/journals");
        if (!directory.exists() && !directory.mkdirs()) {
            throw new IllegalStateException("Could not create the SAF recovery journal directory.");
        }
        return directory;
    }

    private static String getRootIdentity(Uri treeUri) {
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256")
                .digest(treeUri.toString().getBytes(StandardCharsets.UTF_8));
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < 12; index++) {
                builder.append(String.format("%02x", digest[index]));
            }
            return builder.toString();
        } catch (Exception exception) {
            throw new IllegalStateException("Could not derive the SAF root identity.", exception);
        }
    }

    private static File getJournalFile(Activity activity, String transactionId) {
        return new File(getJournalDirectory(activity), transactionId + ".json");
    }

    private static String nullableString(JSONObject object, String name) {
        return object.isNull(name) ? null : object.optString(name, null);
    }

    private static Uri requireModelsDirectory(Activity activity) {
        Uri treeUri = getPersistedTreeUri(activity);
        if (treeUri == null || !hasPersistedPermission(activity, treeUri)) {
            throw new IllegalStateException("No persisted SAF tree permission is available.");
        }
        Uri modelsUri = getOrCreateModelsDirectory(activity, treeUri);
        if (modelsUri == null) {
            throw new IllegalStateException("The OfflineModels directory is unavailable.");
        }
        return modelsUri;
    }

    private static Uri getOrCreateModelsDirectory(Activity activity, Uri treeUri) {
        try {
            Uri existing = findChild(activity, treeUri, MODELS_DIRECTORY);
            if (existing != null) {
                return existing;
            }
            return DocumentsContract.createDocument(
                activity.getContentResolver(),
                asDocumentUri(treeUri),
                DocumentsContract.Document.MIME_TYPE_DIR,
                MODELS_DIRECTORY);
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Could not resolve the models directory", exception);
            return null;
        }
    }

    private static boolean isValidSelectedRoot(Activity activity, Uri treeUri) {
        Uri rootDocumentUri = DocumentsContract.buildDocumentUriUsingTree(
            treeUri, DocumentsContract.getTreeDocumentId(treeUri));
        try (Cursor cursor = activity.getContentResolver().query(
            rootDocumentUri,
            new String[] { DocumentsContract.Document.COLUMN_DISPLAY_NAME },
            null,
            null,
            null)) {
            if (cursor == null || !cursor.moveToFirst()) {
                return false;
            }
            String displayName = cursor.getString(0);
            return "Blocks".equalsIgnoreCase(displayName) ||
                "Open Blocks".equalsIgnoreCase(displayName);
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Selected-root validation failed", exception);
            return false;
        }
    }

    private static Uri findChild(Activity activity, Uri parentUri, String displayName) throws Exception {
        for (DocumentRecord child : queryChildren(activity, parentUri)) {
            if (displayName.equals(child.displayName)) {
                return child.uri;
            }
        }
        return null;
    }

    private static boolean hasModelFile(Activity activity, Uri modelUri) throws Exception {
        for (DocumentRecord child : queryChildren(activity, modelUri)) {
            if (!child.isDirectory && MODEL_FILE_NAME.equals(child.displayName)) {
                return true;
            }
        }
        return false;
    }

    private static List<DocumentRecord> queryChildren(Activity activity, Uri parentUri) throws Exception {
        String parentDocumentId = getDocumentId(parentUri);
        Uri childrenUri = DocumentsContract.buildChildDocumentsUriUsingTree(parentUri, parentDocumentId);
        String[] projection = new String[] {
            DocumentsContract.Document.COLUMN_DOCUMENT_ID,
            DocumentsContract.Document.COLUMN_DISPLAY_NAME,
            DocumentsContract.Document.COLUMN_MIME_TYPE,
            DocumentsContract.Document.COLUMN_LAST_MODIFIED
        };

        List<DocumentRecord> result = new ArrayList<>();
        try (Cursor cursor = activity.getContentResolver().query(
            childrenUri, projection, null, null, null)) {
            if (cursor == null) {
                throw new IllegalStateException("The Documents provider returned a null cursor.");
            }
            while (cursor.moveToNext()) {
                String documentId = cursor.getString(0);
                String displayName = cursor.getString(1);
                String mimeType = cursor.getString(2);
                long lastModified = cursor.isNull(3) ? 0L : cursor.getLong(3);
                Uri uri = DocumentsContract.buildDocumentUriUsingTree(parentUri, documentId);
                result.add(new DocumentRecord(uri, displayName,
                    DocumentsContract.Document.MIME_TYPE_DIR.equals(mimeType), lastModified));
            }
        }
        result.sort(Comparator.comparingLong(record -> record.lastModified));
        return result;
    }

    private static String getDocumentId(Uri uri) {
        List<String> segments = uri.getPathSegments();
        if (segments.contains("document")) {
            return DocumentsContract.getDocumentId(uri);
        }
        return DocumentsContract.getTreeDocumentId(uri);
    }

    private static Uri asDocumentUri(Uri uri) {
        return uri.getPathSegments().contains("document")
            ? uri
            : DocumentsContract.buildDocumentUriUsingTree(uri, DocumentsContract.getTreeDocumentId(uri));
    }

    private static boolean documentExists(Activity activity, Uri uri) {
        if (uri == null) {
            return false;
        }
        try (Cursor cursor = activity.getContentResolver().query(
            uri,
            new String[] { DocumentsContract.Document.COLUMN_DOCUMENT_ID },
            null,
            null,
            null)) {
            return cursor != null && cursor.moveToFirst();
        } catch (Exception exception) {
            return false;
        }
    }

    private static Uri getPersistedTreeUri(Context context) {
        String value = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            .getString(PREF_TREE_URI, null);
        return value == null ? null : Uri.parse(value);
    }

    private static boolean hasPersistedPermission(Activity activity, Uri treeUri) {
        for (android.content.UriPermission permission :
            activity.getContentResolver().getPersistedUriPermissions()) {
            if (permission.getUri().equals(treeUri) &&
                permission.isReadPermission() &&
                permission.isWritePermission()) {
                return true;
            }
        }
        return false;
    }

    private static boolean isReservedName(String displayName) {
        return displayName == null ||
            displayName.startsWith(TEMP_PREFIX) ||
            displayName.startsWith(BACKUP_PREFIX);
    }

    private static boolean isSafeLeafName(String name) {
        return name != null &&
            !name.isEmpty() &&
            !name.contains("/") &&
            !name.contains("\\") &&
            !".".equals(name) &&
            !"..".equals(name);
    }

    private static final class DocumentRecord {
        final Uri uri;
        final String displayName;
        final boolean isDirectory;
        final long lastModified;

        DocumentRecord(Uri uri, String displayName, boolean isDirectory, long lastModified) {
            this.uri = uri;
            this.displayName = displayName;
            this.isDirectory = isDirectory;
            this.lastModified = lastModified;
        }
    }

    public static final class PickerFragment extends Fragment {
        @Override
        public void onCreate(Bundle savedInstanceState) {
            super.onCreate(savedInstanceState);
            setRetainInstance(true);
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION |
                Intent.FLAG_GRANT_WRITE_URI_PERMISSION |
                Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION |
                Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);
            startActivityForResult(intent, PICK_TREE_REQUEST);
        }

        @Override
        public void onActivityResult(int requestCode, int resultCode, Intent data) {
            super.onActivityResult(requestCode, resultCode, data);
            Activity activity = getActivity();
            if (requestCode != PICK_TREE_REQUEST || activity == null) {
                return;
            }

            if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null) {
                accessRequestState = ACCESS_CANCELLED;
                removeSelf(activity);
                return;
            }

            Uri treeUri = data.getData();
            try {
                int flags = data.getFlags() &
                    (Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
                activity.getContentResolver().takePersistableUriPermission(treeUri, flags);
                if (!isValidSelectedRoot(activity, treeUri)) {
                    activity.getContentResolver().releasePersistableUriPermission(treeUri, flags);
                    accessRequestState = ACCESS_FAILED;
                    removeSelf(activity);
                    return;
                }
                SharedPreferences preferences =
                    activity.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
                preferences.edit().putString(PREF_TREE_URI, treeUri.toString()).commit();
                accessRequestState = getOrCreateModelsDirectory(activity, treeUri) == null
                    ? ACCESS_FAILED
                    : ACCESS_GRANTED;
            } catch (Exception exception) {
                accessRequestState = ACCESS_FAILED;
                android.util.Log.w(LOG_PREFIX, "Could not retain selected folder access", exception);
            }
            removeSelf(activity);
        }

        private void removeSelf(Activity activity) {
            activity.getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
        }
    }
}
