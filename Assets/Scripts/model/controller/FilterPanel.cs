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

using com.google.apps.peltzer.client.api_clients.assets_service_client;
using com.google.apps.peltzer.client.menu;
using com.google.apps.peltzer.client.model.main;
using TMPro;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.controller
{
    public class FilterPanel : MonoBehaviour
    {
        public bool m_Allowed = true;

        private bool m_IsOpen;
        public bool IsOpen => m_IsOpen;
        public TextMeshPro m_TitleText;
        public RadioButtonContainer m_OrderByContainer;
        public RadioButtonContainer m_CategoryContainer;
        public Slider m_TriangleCountSlider;

        private PolyMenuMain m_MainMenu;

        public virtual void Enable()
        {
            if (m_IsOpen || !m_Allowed) return; // No change or we've disabled this panel
            m_IsOpen = true;
            gameObject.SetActive(true);
            m_MainMenu = PeltzerMain.Instance.GetPolyMenuMain();
        }

        public virtual void Disable()
        {
            if (!m_IsOpen) return; // No change
            m_IsOpen = false;
            gameObject.SetActive(false);
        }

        public void HandleOk()
        {
            var category = m_CategoryContainer.Value;
            var orderBy = m_OrderByContainer.Value;
            var triangleCount = (int)m_TriangleCountSlider.Value;

            // check if refresh is necessary
            if (m_MainMenu.CurrentQueryParams.Category == category &&
                m_MainMenu.CurrentQueryParams.OrderBy == orderBy &&
                m_MainMenu.CurrentQueryParams.TriangleCountMax == triangleCount)
            {
                Disable();
                return;
            }

            m_MainMenu.SetApiOrderBy(orderBy);
            m_MainMenu.SetApiCategoryFilter(category);
            m_MainMenu.SetApiTriangleCountMax(triangleCount);
            m_MainMenu.RefreshResults();
            Disable();
        }

        public void HandleCancel()
        {
            Disable();
        }

        public void InitControls()
        {
            var menuMain = PeltzerMain.Instance.GetPolyMenuMain();
            ApiQueryParameters currentQueryParams = menuMain.CurrentQueryParams;
            // Show liked time only if we're in the liked tab
            m_OrderByContainer.ShowOptionByIndex(3, menuMain.CurrentCreationType() == PolyMenuMain.CreationType.LIKED);
            m_CategoryContainer.SetInitialOption(currentQueryParams.Category);
            m_OrderByContainer.SetInitialOption(currentQueryParams.OrderBy);
            m_TriangleCountSlider.SetInitialValue(currentQueryParams.TriangleCountMax);

            string titleText;
            if (string.IsNullOrWhiteSpace(currentQueryParams.SearchText))
            {
                titleText = $"Listing: ";
            }
            else
            {
                titleText = $"Searching: ";
            }
            switch (menuMain.CurrentCreationType())
            {
                case PolyMenuMain.CreationType.FEATURED:
                    titleText += "All Models";
                    break;
                case PolyMenuMain.CreationType.LIKED:
                    titleText += "Your Likes";
                    break;
                case PolyMenuMain.CreationType.YOUR:
                    titleText += "Your Uploads";
                    break;
                case PolyMenuMain.CreationType.LOCAL:
                    titleText += "Your Saved Models";
                    break;
            }
            m_TitleText.text = titleText;
        }

        public void UpdateSliderLabel(float value)
        {
            if (value == m_TriangleCountSlider.m_NoLimitSpecialValue)
            {
                m_TriangleCountSlider.hoverName = $"Unlimited faces";
            }
            else
            {
                m_TriangleCountSlider.hoverName = $"Max {(int)value} faces";
            }
        }
    }
}