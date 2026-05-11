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

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core
{
    [TestFixture]
    public class GeometryOperationTest
    {
        [Test]
        public void TestTryGetCurrentFaceReturnsFalseAfterFaceDeletedInSameOperation()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);
            Face originalFace = mesh.GetFace(1);
            MMesh.GeometryOperation operation = mesh.StartOperation();

            operation.ModifyFace(1,
                new List<int>(originalFace.vertexIds).AsReadOnly(),
                originalFace.properties);
            operation.DeleteFace(1);

            Face currentFace;
            Assert.False(operation.TryGetCurrentFace(1, out currentFace));
        }
    }
}
