// Copyright 2020 The Blocks Authors
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

using UnityEngine;
using System.Collections.Generic;

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    ///   Representation of an Axes which is made of three vectors. Each vector represents the right, up, and forward
    ///   orientation of space.
    /// </summary>
    public class Axes
    {
        public static Axes identity = new Axes(Vector3.right, Vector3.up, Vector3.forward);
        public enum Axis { RIGHT, UP, FORWARD };

        public Vector3 right;
        public Vector3 up;
        public Vector3 forward;

        public Axes(Vector3 right, Vector3 up, Vector3 forward)
        {
            this.right = right;
            this.up = up;
            this.forward = forward;
        }

        /// <summary>
        ///   Finds the rotation from one Axes to another.
        /// </summary>
        /// <param name="from">The starting Axes.</param>
        /// <param name="to">The final Axes.</param>
        /// <returns>The rotational difference between the two Axes'.</returns>
        public static Quaternion FromToRotation(Axes from, Axes to)
        {
            // To find the rotation between two Axes we only need to find the difference between two of the axes since a
            // pair of axes must move together to maintain the 90 degree angles between them. So we will start by moving
            // one arbitrary Axes into place. Then applying this change to a second Axes and finding the remaining
            // difference between this Axes and the universal grid axes.

            // Start by finding the rotation difference between the to.up axis and the from.up.
            Quaternion yRotation = Quaternion.FromToRotation(from.up, to.up);

            // Apply the yRotation to the from.right then find the difference between the partially rotated from.right axis
            // and the to.right axis.
            Quaternion residualRotation = Quaternion.FromToRotation(yRotation * from.right, to.right);

            // Combine the rotations to find the rotational difference.
            return residualRotation * yRotation;
        }

        /// <summary>
        /// Given the coplanarVertices of face determine the axes for the face.
        /// </summary>
        /// <param name="coplanarVertices">Vertices of the face.</param>
        /// <returns>The axes that define the face.</returns>
        public static Axes FindAxesForAFace(List<Vector3> coplanarVertices)
        {
            Vector3 forward = FindForwardAxis(coplanarVertices);
            Vector3 right = FindRightAxis(coplanarVertices);
            Vector3 up = FindUpAxis(forward, right);

            return new Axes(right, up, forward);
        }

        /// <summary>
        /// Finds the forward axis given the veritces of a face, which is just the normal out of the origin.
        /// </summary>
        /// <param name="coplanarVertices">The vertices of a face.</param>
        /// <returns>The forward axis.</returns>
        public static Vector3 FindForwardAxis(List<Vector3> coplanarVertices)
        {
            return MeshMath.CalculateNormal(coplanarVertices);
        }

        /// <summary>
        ///   Finds the right axis of a face by comparing all the edges in the face and choosing an edge for the right axis
        ///   that is the most representative of the other edges. Essentially we are trying to find an edge that is
        ///   perpendicular to as many edges as possible so that we can rotate the preview to align to the greatest number
        ///   of edges. Since the forward axis is the face normal any edge is guaranteed to be perpendicular to the normal.
        /// </summary>
        /// <param name="coplanarVertices">The vertices of a face.</param>
        /// <returns>The right axis.</returns>
        public static Vector3 FindRightAxis(List<Vector3> coplanarVertices)
        {
            EdgeInfo mostRepresentativeEdge = MeshMath.FindMostRepresentativeEdge(coplanarVertices);
            return mostRepresentativeEdge.edgeVector.normalized;
        }

        /// <summary>
        ///   Finds the up axis which is just the axis perpendicular to the right and forward axis. We use the left hand
        ///   rule to make sure the up axis points the right way so snapGrid.forward, up and right are related the same
        ///   as the universal Vector3.forward, up and right.
        /// </summary>
        /// <param name="right">The right axis.</param>
        /// <param name="forward">The forward axis.</param>
        /// <returns>The cross product of the right and forward axis.</returns>
        public static Vector3 FindUpAxis(Vector3 right, Vector3 forward)
        {
            return Vector3.Cross(forward, right).normalized;
        }
    }

}
