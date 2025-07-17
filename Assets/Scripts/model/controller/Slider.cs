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
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace com.google.apps.peltzer.client.model.controller
{
    public class Slider : PolyMenuButton
    {
        private static readonly int SHADER_SLIDE_VALUE_PROP = Shader.PropertyToID("_SlideValue");
        public UnityEvent<float> m_ActionOnFullUpdate;
        public UnityEvent<float> m_ActionEveryUpdate;
        public MeshRenderer m_SliderRenderer;
        public TextMeshPro m_Label;
        public float m_Minimum = 0;
        public float m_Maximum = 1;
        public float m_Step = 0.1f;
        public int m_UpdateInterval = 1;
        public bool m_UpdateOnlyOnRelease;
        public bool m_MaxMeansNoLimit;
        public int m_NoLimitSpecialValue = -9999;

        public float Value
        {
            get
            {
                if (m_MaxMeansNoLimit && m_NormalizedValue >= 1)
                {
                    return m_NoLimitSpecialValue;
                }
                float val = Mathf.Lerp(m_Minimum, m_Maximum, m_NormalizedValue);
                val = Mathf.Round(val / m_Step) * m_Step;
                return val;
            }
        }

        [NonSerialized] private float m_NormalizedValue;

        private bool m_IsDragging;
        private Material m_SliderMaterial;
        private float m_LastUpdateFrame;

        public override void Start()
        {
            base.Start();
            PeltzerMain.Instance.peltzerController.PeltzerControllerActionHandler += ControllerEventHandler;
        }

        public override void Update()
        {
            m_ActionEveryUpdate.Invoke(Value);
            if (m_IsDragging && !m_UpdateOnlyOnRelease)
            {
                if (Time.frameCount - m_LastUpdateFrame > m_UpdateInterval)
                {
                    m_LastUpdateFrame = Time.frameCount;
                    m_ActionOnFullUpdate.Invoke(Value);
                }
            }
        }

        private static float GetNormalizedLocalPosition(RaycastHit hit)
        {
            // Convert the hit point to the local space of the hit object.
            Transform hitTransform = hit.transform;
            Vector3 localHitPoint = hitTransform.InverseTransformPoint(hit.point);

            // Get the collider component.
            Collider collider = hitTransform.GetComponent<Collider>();

            // Convert the collider's bounds center and size into local space.
            Vector3 localCenter = hitTransform.InverseTransformPoint(collider.bounds.center);
            Vector3 localSize = hitTransform.InverseTransformVector(collider.bounds.size);
            localSize.x *= 0.8f;
            float halfWidth = Mathf.Abs(localSize.x) / 2f;

            // Compute the minimum and maximum x values based on the local center and half width.
            float minX = localCenter.x - halfWidth;
            float maxX = localCenter.x + halfWidth;

            // Calculate and return the normalized value.
            float normalized = Mathf.InverseLerp(minX, maxX, localHitPoint.x);
            return normalized;
        }

        public void SetHitPoint(RaycastHit hit)
        {
            if (m_IsDragging)
            {
                float normalizedLocalPosition = GetNormalizedLocalPosition(hit);
                float val = Mathf.Lerp(m_Minimum, m_Maximum, normalizedLocalPosition);
                val = Mathf.Round(val / m_Step) * m_Step;
                SetInitialValue(val);
            }
        }

        public void SetInitialValue(float val)
        {
            if (val == m_NoLimitSpecialValue && m_MaxMeansNoLimit)
            {
                val = m_Maximum;
            }
            val = Mathf.Round(val / m_Step) * m_Step;
            m_NormalizedValue = Mathf.InverseLerp(m_Minimum, m_Maximum, val);
            if (m_SliderMaterial == null)
            {
                m_SliderMaterial = m_SliderRenderer.material;
            }
            m_SliderMaterial.SetFloat(SHADER_SLIDE_VALUE_PROP, m_NormalizedValue);
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
                        m_ActionOnFullUpdate.Invoke(Value);
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

        public virtual void SetLabelText(string text)
        {
            m_Label.text = text;
        }
    }
}