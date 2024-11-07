// Copyright 2024 The Open Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.util;
using NativeTrees;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Class wrapping NativeTrees functions which implement our collision system.
/// </summary>
public static class BurstSpatialFunction
{
    private static List<NativeOctree<int>> _octrees = new();

    public static int AllocSpatialPartitioner()
    {
        var octree = new NativeOctree<int>();
        _octrees.Add(octree);
        return _octrees.Count - 1;
    }

    public static void SpatialPartitionerAddItem(int idx, int itemId, Vector3 center, Vector3 extents)
    {
        _octrees[idx].InsertPoint(itemId, center);
    }

    public static void SpatialPartitionerUpdateAll(int idx, object itemIds)
    {
        var octree = new NativeOctree<int>();
        octree = _octrees[idx];
        jgtuu
    }

    public static int SpatialPartitionerIntersectedBy(int idx, Vector3 testCenter, Vector3 testExtents, int[] returnArray, int returnArrayMaxSize)
    {
        AABB range = new AABB(testCenter, testExtents);
        var results = new NativeParallelHashSet<int>(returnArrayMaxSize, Allocator.TempJob);
        RangeAABBUnique(_octrees[idx], range, results);
        returnArray = results.ToArray();
    }

    public static void RangeAABBUnique<T>(this NativeOctree<T> octree, AABB range, NativeParallelHashSet<T> results)
        where T : unmanaged, IEquatable<T>
    {
        var vistor = new RangeAABBUniqueVisitor<T>()
        {
            results = results
        };

        octree.Range(range, ref vistor);
    }

    struct RangeAABBUniqueVisitor<T> : IOctreeRangeVisitor<T> where T : unmanaged, IEquatable<T>
    {
        public NativeParallelHashSet<T> results;

        public bool OnVisit(T obj, AABB objBounds, AABB queryRange)
        {
            // check if our object's AABB overlaps with the query AABB
            if (objBounds.Overlaps(queryRange))
                results.Add(obj);

            return true; // always keep iterating, we want to catch all objects
        }
    }
}

public class BurstSpatial<T> : CollisionSystem<T>
{

    // A unique handle that identifies this collision system
    private int spatialPartitionId;
    // Used for super cheap id allocation - we use this id and increment.
    private int nextHandleId = 0;
    // A mapping from the items in this system to their numeric handle
    private Dictionary<T, int> itemIds = new Dictionary<T, int>();
    // A mapping of numeric handles to items in the system.
    private Dictionary<int, T> idsToItems = new Dictionary<int, T>();
    // A mapping of items in the system to their Bounds.
    private Dictionary<T, Bounds> itemBounds = new Dictionary<T, Bounds>();
    // A preallocated array for retrieving results
    private int[] results = new int[SpatialIndex.MAX_INTERSECT_RESULTS];

    public BurstSpatial()
    {
        spatialPartitionId = BurstSpatialFunction.AllocSpatialPartitioner();
    }

    /// <summary>
    /// Adds an item to the CollisionSystem.
    /// </summary>
    public void Add(T item, Bounds bounds)
    {
        int id = nextHandleId++;
        itemIds[item] = id;
        idsToItems[id] = item;
        itemBounds[item] = bounds;
        BurstSpatialFunction.SpatialPartitionerAddItem(spatialPartitionId, id, bounds.center, bounds.extents);
    }

    /// <summary>
    /// Remove an item from the system.
    /// </summary>
    /// <param name="item">Item to remove.</param>
    /// <exception cref="System.Exception">
    ///  Thrown when the item isn't in the tree.</exception>
    public void Remove(T item)
    {
        int id = itemIds[item];
        itemIds.Remove(item);
        idsToItems.Remove(id);
        itemBounds.Remove(item);
        BurstSpatialFunction.SpatialPartitionerUpdateAll(spatialPartitionId, itemIds);
    }

    /// <summary>
    /// Returns whether the item is tracked in this system.
    /// </summary>
    public bool HasItem(T item)
    {
        bool inItems = itemIds.ContainsKey(item);
        return inItems;
    }

    /// <summary>
    /// Checks whether the supplied Bounds intersects anything in the system, and returns a HashSet
    /// of intersection objects.  Returns true if there were any intersections.
    /// </summary>
    public bool IntersectedBy(Bounds bounds, out HashSet<T> items,
      int limit = SpatialIndex.MAX_INTERSECT_RESULTS)
    {
        int numResults = BurstSpatialFunction.SpatialPartitionerIntersectedBy(spatialPartitionId, bounds.center,
          bounds.extents, results, limit);
        items = new HashSet<T>();
        for (int i = 0; i < numResults; i++)
        {
            items.Add(idsToItems[results[i]]);
        }
        return items.Count > 0;
    }

    /// <summary>
    /// Checks whether the supplied Bounds intersects anything in the system, and fills the supplied preallocated Hashset
    /// with intersected items.  Returns true if there were any intersections.
    /// </summary>
    public bool IntersectedByPreallocated(Bounds bounds, ref HashSet<T> items,
      int limit = SpatialIndex.MAX_INTERSECT_RESULTS)
    {
        int numResults = BurstSpatialFunction.SpatialPartitionerIntersectedBy(spatialPartitionId, bounds.center,
          bounds.extents, results, limit);
        for (int i = 0; i < numResults; i++)
        {
            items.Add(idsToItems[results[i]]);
        }
        return items.Count > 0;
    }

    public Bounds BoundsForItem(T item)
    {
        return itemBounds[item];
    }
}
