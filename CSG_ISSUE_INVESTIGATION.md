# CSG Missing Faces Investigation

## Problem Statement

CSG subtract operations occasionally produce results with missing faces - specifically, the faces that should form the cavity walls where material was subtracted are absent, creating gaps/holes in the geometry.

**Key Characteristic:** The orientation of the cutting shape is a critical factor in whether the operation succeeds or fails.

## Root Cause Analysis

### What We Discovered

1. **Algorithm Assumption Violation**
   - The CSG algorithm assumes all polygons are convex (documented at CsgOperations.cs:720: "CsgPolygons should be convex and have no holes")
   - `CoplanarFaceMerger.MergeCoplanarFaces()` is called after every CSG operation (CsgOperations.cs:165)
   - Merged coplanar faces can be **concave**
   - When concave faces are used as input to subsequent CSG operations, the algorithm breaks down

2. **Classification Failure Pattern**
   - Analysis of logs showed the cutting object (rightObj) having 0 INSIDE faces when it should have several
   - Example from logs:
     ```
     leftObj classification - INSIDE:5 OUTSIDE:16
     rightObj classification - INSIDE:0 OUTSIDE:15  ← All faces classified as OUTSIDE
     Selected from left (OUTSIDE+OPPOSITE): 16, from right (INSIDE inverted): 0  ← No cavity faces
     ```

3. **Why Concave Faces Break CSG**
   - The barycenter (center point) of a concave polygon can be **outside** the polygon itself
   - Raycasting from the barycenter in the normal direction is used for inside/outside classification
   - For concave polygons, this raycast may pass through the polygon's own geometry
   - The orientation dependency occurs because different orientations expose different aspects of the concave geometry

### Investigation Process

Added extensive debug logging to track:
- Polygon counts before/after splitting
- Classification results (UNKNOWN, INSIDE, OUTSIDE, SAME, OPPOSITE)
- Raycast statistics (hits vs successful inside tests)
- Which polygons used raycast vs vertex-based classification

Findings:
- Raycasts were hitting planes but intersection points were falling outside polygon boundaries
- Only 2-4 polygons per object were classified via raycast
- The rest inherited status via vertex propagation
- One misclassified polygon could propagate wrong status to many others

## Bug Fix Applied

**File:** `Assets/Scripts/model/csg/PolygonSplitter.cs`
**Line:** 102
**Issue:** Typo causing incorrect plane crossing detection

```csharp
// BEFORE (Bug):
float[] distBtoA = DistanceFromVertsToPlane(polyB, polyA.plane);
if (!CrossesPlane(distAtoB))  // ← Checking distAtoB again instead of distBtoA
{
    return false;
}

// AFTER (Fixed):
float[] distBtoA = DistanceFromVertsToPlane(polyB, polyA.plane);
if (!CrossesPlane(distBtoA))  // ← Now correctly checking distBtoA
{
    return false;
}
```

**Impact:** This bug caused many valid polygon intersections to be incorrectly skipped during splitting, leading to incomplete splits and potentially contributing to classification failures.

## Attempted Solutions (Failed)

### 1. Triangulate All Faces Before CSG
**Approach:** Modified `GeneratePolygonsForFace()` to use `FaceTriangulator.TriangulateFace()`, ensuring all CsgPolygons are triangles (always convex).

**Result:** FAILED - Caused overlapping/glitchy geometry

**Why it failed:**
- Likely due to normal orientation issues with triangulated faces
- Attempting to preserve the original face normal (using `CsgPolygon(vertices, props, normal)`) still produced artifacts
- The triangulator may produce triangles with inconsistent winding relative to the original face

### 2. Accept Boundary Hits in Raycast
**Approach:** Changed `if (isInside > 0)` to `if (isInside >= 0)` in the raycast code to accept points exactly on polygon boundaries.

**Result:** FAILED - Did not resolve the missing faces issue

**Why it failed:**
- The actual problem was raycasts hitting planes but finding intersection points outside all polygon boundaries (isInside = -1), not on boundaries (isInside = 0)
- After splitting creates many small polygons, ray-plane intersections often land in the plane but outside the actual polygon area

### 3. Retry Raycasts with Perturbation
**Approach:** Added logic to retry raycasts with perturbed directions when hits occurred but no valid inside points were found.

**Result:** FAILED - Perturbation attempts (up to 5) still failed to find valid hits

**Reverted:** This change was removed along with the failed triangulation approach

## Code Architecture Notes

### CSG Operation Flow
1. `DoCsgOperation()` - Entry point, handles bounds checking and coordinate transformation
2. Splitting phase:
   - `SplitObject(leftObj, rightObj)` - Split left by right's planes
   - `SplitObject(rightObj, leftObj)` - Split right by left's planes
   - `SplitObject(leftObj, rightObj)` - Split left again (per Laidlaw-1986-CSG.pdf algorithm)
3. Classification phase:
   - `ClassifyPolygons()` - Determine which polygons are INSIDE/OUTSIDE/etc
   - Uses raycasting for polygons with UNKNOWN/BOUNDARY vertices
   - Uses vertex status propagation for others
4. Selection phase:
   - For SUBTRACT: Select OUTSIDE+OPPOSITE from left, INSIDE (inverted) from right
   - `FromPolys()` - Convert CsgPolygons back to MMesh
5. Post-processing:
   - `CoplanarFaceMerger.MergeCoplanarFaces()` - Creates potentially concave faces

### Key Components

**FaceTriangulator.cs**
- Implements ear-clipping triangulation algorithm
- Handles concave polygons and holes
- Used for rendering but NOT currently used for CSG input

**CsgMath.IsInside()**
- Tests if a point is inside a polygon
- Returns: 1 (inside), 0 (on boundary), -1 (outside)
- Critical for raycast classification

**Vertex Status Propagation**
- Vertices connected by polygon edges propagate their INSIDE/OUTSIDE status
- Efficient but can propagate misclassifications from a single bad raycast

## Recommendations for Future Investigation

1. **Investigate the orientation dependency**
   - Why does the cutting object's orientation affect classification?
   - Are there specific angles/rotations that consistently fail?
   - Could there be a winding order or normal flipping issue?

2. **Consider alternative approaches**
   - Use a robust 3D geometry library (CGAL, Carve, libigl) instead of custom CSG
   - Implement convex decomposition before CSG operations
   - Pre-triangulate faces but solve the normal/winding consistency issue
   - Don't merge coplanar faces until all CSG operations are complete

3. **Deep dive into classification logic**
   - The raycast classification algorithm (ClassifyPolygonUsingRaycast) is complex
   - Paper reference: http://vis.cs.brown.edu/results/videos/bib/pdf/Laidlaw-1986-CSG.pdf
   - May need computational geometry expertise to debug

4. **Test with simple cases**
   - Create minimal reproduction: simple cube subtract cube
   - Rotate cutting cube through various angles
   - Document which orientations work vs fail
   - Look for patterns in the failure cases

## System Context

- **Unity Version:** 2022.3.42f1 LTS
- **CSG Paper Reference:** Laidlaw-1986-CSG.pdf
- **Related Files:**
  - `Assets/Scripts/model/csg/CsgOperations.cs` - Main CSG logic
  - `Assets/Scripts/model/csg/PolygonSplitter.cs` - Polygon splitting
  - `Assets/Scripts/model/csg/CsgMath.cs` - Geometric utilities
  - `Assets/Scripts/model/import/CoplanarFaceMerger.cs` - Face merging
  - `Assets/Scripts/model/render/FaceTriangulator.cs` - Triangulation

## Status

**Current State:**
- One bug fix applied (PolygonSplitter.cs:102)
- Missing faces issue still occurs
- Orientation-dependent behavior not resolved

**Next Steps:** Recommend seeking expertise from someone with computational geometry background or considering replacement with proven CSG library.

---
*Investigation Date: 2025-01-19*
