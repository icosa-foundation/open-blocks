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
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;
using UnityEngine;
using UnityEngine.Events;

namespace com.google.apps.peltzer.client.model.controller
{
    public class Slider : PolyMenuButton
    {
        private static readonly int SHADER_SLIDE_VALUE_PROP = Shader.PropertyToID("_SlideValue");
        public UnityEvent<float> m_Action;
        public MeshRenderer m_SliderRenderer;
        public float m_Minimum = 0;
        public float m_Maximum = 1;
        public float m_Step = 0.1f;
        public int m_UpdateInterval = 1;
        public bool m_UpdateOnlyOnRelease;

        [NonSerialized] public float m_Value;
        [NonSerialized] public float m_NormalizedValue;

        private bool m_IsDragging;
        private Material m_SliderMaterial;
        private float m_LastUpdateFrame;

        public override void Start()
        {
            base.Start();
            PeltzerMain.Instance.peltzerController.PeltzerControllerActionHandler += ControllerEventHandler;
            m_SliderMaterial = m_SliderRenderer.material;
        }

        public override void Update()
        {
            if (m_IsDragging && !m_UpdateOnlyOnRelease)
            {
                if (Time.frameCount - m_LastUpdateFrame > m_UpdateInterval)
                {
                    m_LastUpdateFrame = Time.frameCount;
                    m_Action.Invoke(m_Value);
                }
            }
        }

        private static float GetNormalizedLocalPosition(RaycastHit hit)
        {
            // Convert world hit point to local space of the hit object
            Transform hitTransform = hit.transform;
            Vector3 localHitPoint = hitTransform.InverseTransformPoint(hit.point);

            Collider collider = hit.collider;
            Bounds localBounds = collider.bounds;
            Vector3 localMin = hitTransform.InverseTransformPoint(localBounds.min);
            Vector3 localMax = hitTransform.InverseTransformPoint(localBounds.max);

            // Calculate the normalized positions (0 to 1)
            var normalized = Mathf.InverseLerp(localMin.x, localMax.x, localHitPoint.x);
            // Fudge factor because my maths is off somewhere
            float fudge = 0.1f;
            normalized = Mathf.InverseLerp(0 + fudge, 1 - fudge, normalized);
            return normalized;
        }

        public void SetHitPoint(RaycastHit hit)
        {
            if (m_IsDragging)
            {
                float normalizedLocalPosition = GetNormalizedLocalPosition(hit);
                m_Value = Mathf.Lerp(m_Minimum, m_Maximum, normalizedLocalPosition);
                m_Value = Mathf.Round(m_Value / m_Step) * m_Step;
                m_NormalizedValue = Mathf.InverseLerp(m_Minimum, m_Maximum, m_Value);
                m_SliderMaterial.SetFloat(SHADER_SLIDE_VALUE_PROP, m_NormalizedValue);
            }
        }

        private void ControllerEventHandler(object sender, ControllerEventArgs args)
        {
            if (!ActionIsAllowed()) return;
            if (args.ControllerType != ControllerType.PELTZER) return;
            if (args.ButtonId != ButtonId.Trigger) return;
            switch (args.Action)
            {
                case ButtonAction.DOWN:
                    m_IsDragging = true;
                    break;
                case ButtonAction.UP:
                    m_IsDragging = false;
                    if (m_UpdateOnlyOnRelease)
                    {
                        m_Action.Invoke(m_Value);
                    }
                    break;
                case ButtonAction.NONE:
                    return;
            }
        }

        /// <summary>
        /// Returns whether or not action is currently allowed.
        /// </summary>
        /// <returns>Whether or not action is allowed.</returns>
        internal bool ActionIsAllowed()
        {
            return PeltzerMain.Instance.restrictionManager.menuActionsAllowed;
        }
    }
}