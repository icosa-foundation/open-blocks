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

using com.google.apps.peltzer.client.model.main;
using UnityEngine;
using UnityEngine.Events;

namespace com.google.apps.peltzer.client.model.controller
{
    public class ActionButton : PolyMenuButton
    {
        public UnityEvent m_Action;

        /// <summary>
        /// Returns whether or not action is currently allowed.
        /// </summary>
        /// <returns>Whether or not action is allowed.</returns>
        internal bool ActionIsAllowed()
        {
            return PeltzerMain.Instance.restrictionManager.menuActionsAllowed;
        }

        public override void ApplyMenuOptions(PeltzerMain main)
        {
            if (!ActionIsAllowed()) return;
            m_Action.Invoke();
            main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);
            StartBump();
        }

        public void Enable(bool enable)
        {
            isActive = enable;
            var icon = transform.GetChild(0).GetComponent<SpriteRenderer>();
            icon.color = enable ? Color.white : Color.gray;
        }
    }
}