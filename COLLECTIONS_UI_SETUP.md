# Collections Support - UI Setup Instructions

The backend code for collections support has been implemented. To complete the integration, you need to add a UI toggle button in the Unity Editor.

## What's Been Implemented (Backend)

✅ **API Integration**
- `GetCollections()` - Fetch user's collections (`/users/me/collections`)
- `GetFeaturedCollections()` - Fetch featured collections (`/collections`)
- `GetLikedCollections()` - Fetch liked collections (`/users/me/likedcollections`)

✅ **State Management**
- `ContentType` enum (MODELS, COLLECTIONS)
- Content type tracking per creation type (YOUR, FEATURED, LIKED)
- `ToggleContentType()` method to switch between Models/Collections
- Dynamic query parameter selection based on content type

✅ **UI Support Code**
- `SelectableContentTypeToggleMenuItem.cs` - Button script for the toggle
- `UpdateContentTypeToggle()` - Updates toggle visibility and text
- Integration with `RefreshPolyMenu()` and `ChangeMenu()`

## What Needs to Be Added (Unity Editor)

You need to create a UI button in the Unity Editor to toggle between Models and Collections.

### Location in Scene Hierarchy

The button should be added here:
```
PolyMenu
└── Models
    └── ContentTypeToggle (new GameObject)
        └── txt (TextMeshPro component)
```

### Setup Steps

1. **Open the Unity Scene** containing the PolyMenu

2. **Navigate to**: `PolyMenu > Models`

3. **Create Toggle Button**:
   - Right-click on "Models" → Create Empty GameObject
   - Name it: `ContentTypeToggle`
   - Position it near the pagination controls or filter panel (adjust to your design)

4. **Add Child Text Element**:
   - Right-click on `ContentTypeToggle` → UI → TextMeshPro
   - Name it: `txt`
   - Set the initial text to: "Models"

5. **Add Script Component**:
   - Select `ContentTypeToggle`
   - Click "Add Component"
   - Search for and add: `SelectableContentTypeToggleMenuItem`
   - Set `isActive` to `true` in the inspector

6. **Style the Button**:
   - Add any visual elements (background, icon, etc.) following your existing UI patterns
   - Look at the pagination buttons for reference (`Models/Pagination/Left` and `Models/Pagination/Right`)
   - You may want to add:
     - A background panel
     - An icon sprite renderer
     - Hover effects (if applicable)

### Optional Enhancements

Consider copying the pagination button structure for consistency:
```
ContentTypeToggle
├── panel (with SelectableContentTypeToggleMenuItem script)
│   ├── ic (SpriteRenderer for icon - optional)
│   └── txt (TextMeshPro for "Models"/"Collections" text)
```

## Testing

Once the UI element is added:

1. **Start the application**
2. **Open the PolyMenu**
3. **Navigate to**: Featured, Your Models, or Liked sections
4. **Click the toggle button** - it should switch between "Models" and "Collections"
5. **Verify**:
   - The text changes between "Models" and "Collections"
   - The content refreshes to show collections instead of models
   - The toggle is hidden when viewing "Local" section (no collections support)

## Technical Details

### How It Works

When the user clicks the toggle button:
1. `SelectableContentTypeToggleMenuItem.ApplyMenuOptions()` is called
2. This calls `PolyMenuMain.ToggleContentType()`
3. The content type state is toggled for the current section
4. `RefreshPolyMenu()` is called to reload with collections/models
5. `UpdateContentTypeToggle()` updates the button text

### API Endpoints Used

| Content Type | Creation Type | Endpoint |
|-------------|---------------|----------|
| Collections | FEATURED | `/collections` |
| Collections | YOUR | `/users/me/collections` |
| Collections | LIKED | `/users/me/likedcollections` |
| Models | FEATURED | `/assets` (featured) |
| Models | YOUR | `/users/me/assets` |
| Models | LIKED | `/users/me/likedassets` |

### Code Files Modified

- `Assets/Scripts/menu/PolyMenuMain.cs` - Added ContentType enum, toggle logic, UI references
- `Assets/Scripts/api_clients/assets_service_client/AssetsServiceClient.cs` - Added collection API methods
- `Assets/Scripts/zandria/ZandriaCreationsManager.cs` - Added content type checking for API calls
- `Assets/Scripts/model/controller/FilterPanel.cs` - Dynamic titles based on content type
- `Assets/Scripts/model/controller/SelectableContentTypeToggleMenuItem.cs` - **NEW** - Toggle button script

## Questions?

If you need help with the Unity Editor setup or want to customize the UI layout, let me know!
