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
using UnityEngine.Events;

namespace com.google.apps.peltzer.client.model.controller
{
    public class Slider : PolyMenuButton
    {
        public UnityEvent<float> m_Action;
        public float m_Minimum = 0;
        public float m_Maximum = 1;
        public float m_Step = 0.1f;

        private float m_Value;
        private bool m_IsDragging;

        public override void Start()
        {
            base.Start();
            PeltzerMain.Instance.peltzerController.PeltzerControllerActionHandler += ControllerEventHandler;
        }

        public override void Update()
        {

        }

        private void ControllerEventHandler(object sender, ControllerEventArgs args)
        {
            if (args.ControllerType != ControllerType.PELTZER) return;
            if (args.ButtonId != ButtonId.Trigger) return;
            if (args.Action == ButtonAction.UP) return;
            HandleTriggerRelease();
        }

        private void HandleTriggerRelease()
        {
            if (!ActionIsAllowed()) return;
            m_IsDragging = false;
            m_Action.Invoke(m_Value);
            StartBump();
        }

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
            m_IsDragging = true;
            StartBump();
        }
    }
}