﻿// Copyright 2024 The Open Blocks Authors
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

namespace com.google.apps.peltzer.client.model.controller
{
    public class RadioButtonOption : PolyMenuButton
    {
        private RadioButtonContainer m_RadioButtonContainer;

        public override void Start()
        {
            base.Start();
            m_RadioButtonContainer = transform.parent.GetComponent<RadioButtonContainer>();
        }

        /// <summary>
        /// Returns whether or not action is currently allowed.
        /// </summary>
        /// <returns>Whether or not action is allowed.</returns>
        internal bool ActionIsAllowed()
        {
            return m_RadioButtonContainer.ActionIsAllowed();
        }

        public override void ApplyMenuOptions(PeltzerMain main)
        {
            if (!ActionIsAllowed()) return;
            if (isActive) return;
            m_RadioButtonContainer.ActivateOption(main, this);
            StartBump();
        }
    }
}