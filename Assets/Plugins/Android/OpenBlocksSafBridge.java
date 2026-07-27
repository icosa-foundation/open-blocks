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
            if (treeUri == null || !hasPersistedPermission(activity, treeUri)) {
                return false;
            }
            return getOrCreateModelsDirectory(activity, treeUri) != null;
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Readiness check failed", exception);
            return false;
        }
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
            if (temporaryUri != null) {
                writeJournal(activity, transactionId, "WritingTemporary",
                    temporaryUri.toString(), null, null, null);
            }
            return temporaryUri == null ? null : temporaryUri.toString();
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

    public static boolean abandonModelWrite(Activity activity, String temporaryId) {
        return deleteDocument(activity, temporaryId);
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
                String state = record.optString("state");
                String temporaryId = nullableString(record, "temporaryId");
                String installedId = nullableString(record, "destinationId");
                String backupId = nullableString(record, "backupId");
                String displayName = nullableString(record, "displayName");

                boolean recovered;
                if ("WritingTemporary".equals(state) || "TemporaryComplete".equals(state)) {
                    recovered = deleteDocument(activity, temporaryId);
                } else if ("OriginalBackedUp".equals(state)) {
                    String replacement = temporaryId == null
                        ? null
                        : installReplacement(activity, temporaryId, displayName);
                    if (replacement != null) {
                        recovered = deleteDocument(activity, backupId);
                    } else {
                        recovered = restoreBackup(activity, backupId, displayName);
                    }
                } else if ("ReplacementInstalled".equals(state)) {
                    recovered = documentExists(activity, Uri.parse(installedId)) &&
                        deleteDocument(activity, backupId);
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
            JSONObject record = new JSONObject();
            record.put("version", 1);
            record.put("transactionId", transactionId);
            record.put("state", state);
            record.put("temporaryId", temporaryId == null ? JSONObject.NULL : temporaryId);
            record.put("destinationId", destinationId == null ? JSONObject.NULL : destinationId);
            record.put("backupId", backupId == null ? JSONObject.NULL : backupId);
            record.put("displayName", displayName == null ? JSONObject.NULL : displayName);
            record.put("updatedUtcMillis", System.currentTimeMillis());

            File journal = getJournalFile(activity, transactionId);
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
        File directory = new File(activity.getFilesDir(), "OpenBlocksSafRecovery/journals");
        if (!directory.exists() && !directory.mkdirs()) {
            throw new IllegalStateException("Could not create the SAF recovery journal directory.");
        }
        return directory;
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
                treeUri,
                DocumentsContract.Document.MIME_TYPE_DIR,
                MODELS_DIRECTORY);
        } catch (Exception exception) {
            android.util.Log.w(LOG_PREFIX, "Could not resolve the models directory", exception);
            return null;
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

    private static List<DocumentRecord> queryChildren(Activity activity, Uri parentUri) throws Exception {
        String parentDocumentId = DocumentsContract.getDocumentId(parentUri);
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
