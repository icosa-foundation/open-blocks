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
    ///   A command represents a mutation of the Model. For example, adding, moving, modifying or deleting meshes
    ///   are commands. In general, UI code should only mutate the Model by using a Command because that allows
    ///   undo/redo to work correctly.
    /// </summary>
    public interface Command
    {
        /// <summary>
        ///   Mutate the model with this command.
        /// </summary>
        /// <param name="model">The model.</param>
        void ApplyToModel(Model model);

        /// <summary>
        ///   Create a command that will undo this Command.  This will be
        ///   called before ApplyToModel so that it will have the desired
        ///   end state available to it.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>The undo command.</returns>
        Command GetUndoCommand(Model model);
    }

    /// <summary>
    ///   Implemented by Commands that retain a non-trivial amount of memory - large collections, serialized
    ///   mesh snapshots and the like - so that the undo/redo history byte budget
    ///   (see Model.EnforceHistoryByteBudget) can account for them and trim history that would otherwise
    ///   grow without bound.
    ///
    ///   Commands that only hold a few scalar fields do not need to implement this: the budget charges every
    ///   command a small flat overhead regardless. Implement it whenever a command holds a collection or
    ///   buffer whose size scales with the size of the model or the user's selection.
    /// </summary>
    public interface ICommandWithRetainedMemory
    {
        /// <summary>
        ///   Estimated heap memory retained by this command, in bytes, on top of the flat per-command
        ///   overhead that the budget already charges.
        ///
        ///   This only needs to be a good order-of-magnitude estimate - it exists to keep history from
        ///   growing unbounded, not to account for memory precisely. Only count memory that would actually
        ///   be reclaimed by discarding this command: references to objects owned by something else (scene
        ///   GameObjects, textures owned by a manager, meshes still in the model) should NOT be counted,
        ///   since dropping the command would not free them.
        /// </summary>
        long GetRetainedMemoryBytes();
    }
}
