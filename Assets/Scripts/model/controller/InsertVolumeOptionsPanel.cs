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
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools;
using UnityEngine;
using UnityEngine.Events;

namespace com.google.apps.peltzer.client.model.controller
{
    public class InsertVolumeOptionsPanel : ToolOptionsPanel
    {

        void Start()
        {
            PeltzerMain.Instance.peltzerController.shapesMenu.ShapeMenuItemChangedHandler += ShapeChangedEventHandler;
        }

        void OnDestroy()
        {
            PeltzerMain.Instance.peltzerController.shapesMenu.ShapeMenuItemChangedHandler -= ShapeChangedEventHandler;
        }

        private void ShapeChangedEventHandler(int newMenuItemId)
        {
            Primitives.Shape selectedVolumeShape = (Primitives.Shape)newMenuItemId;
            switch (selectedVolumeShape)
            {

                case Primitives.Shape.CONE:
                    break;
                case Primitives.Shape.SPHERE:
                    break;
                case Primitives.Shape.CUBE:
                    break;
                case Primitives.Shape.CYLINDER:
                    break;
                case Primitives.Shape.TORUS:
                    break;
                case Primitives.Shape.ICOSAHEDRON:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void Enable(ControllerMode mode)
        {
            base.Enable(mode);
            var peltzerController = PeltzerMain.Instance.peltzerController;
            ShapeChangedEventHandler(peltzerController.shapesMenu.CurrentItemId);
        }
    }
}
