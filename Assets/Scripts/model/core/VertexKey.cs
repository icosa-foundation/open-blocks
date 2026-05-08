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

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    /// A canonical id for a vertex, which includes the id of the mesh it belongs to.
    /// </summary>
    public readonly struct VertexKey : System.IEquatable<VertexKey>
    {
        private readonly int _meshId;
        private readonly int _vertexId;

        public VertexKey(int meshId, int vertexId)
        {
            _meshId = meshId;
            _vertexId = vertexId;
        }

        public override bool Equals(object obj) => obj is VertexKey other && Equals(other);

        public bool Equals(VertexKey otherKey)
        {
            return _vertexId == otherKey._vertexId
              && _meshId == otherKey._meshId;
        }

        // 31 is a good number: http://stackoverflow.com/questions/299304/why-does-javas-hashcode-in-string-use-31-as-a-multiplier
        public override int GetHashCode() => (151 + _meshId) * 31 + _vertexId;

        public static bool operator ==(VertexKey a, VertexKey b) => a.Equals(b);
        public static bool operator !=(VertexKey a, VertexKey b) => !a.Equals(b);

        public int meshId => _meshId;
        public int vertexId => _vertexId;
    }
}
