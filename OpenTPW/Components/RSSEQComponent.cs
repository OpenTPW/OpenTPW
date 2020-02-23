﻿using ECSEngine.Components;
using ImGuiNET;
using OpenTPW.RSSEQ;
using System;
using System.Numerics;

namespace OpenTPW.Components
{
    public class RSSEQComponent : Component<RSSEQComponent>
    {
        private VM vm; // probably shouldn't create a new VM instance for every ride
        private bool showImGUIWindow;
        private string disassembly;
        private string log;

        public RSSEQComponent(string pathToRSE)
        {
            vm = new VM(pathToRSE);
            disassembly = vm.disassembly;
        }

        public RSSEQComponent(byte[] rseData)
        {
            vm = new VM(rseData);
            disassembly = vm.disassembly;
        }

        public override void RenderImGUI()
        {
            if (ImGui.Button("Toggle window"))
            {
                showImGUIWindow = !showImGUIWindow;
            }

            if (showImGUIWindow)
            {
                ImGui.Begin("RSSEQ VM");

                if (ImGui.Button("Step thru"))
                {
                    vm.Step();
                }

                ImGui.LabelText("", "Config");
                // TODO: condense these into a list somehow
                ImGui.InputInt("Stack Size", ref vm.config.stackSize);
                ImGui.InputInt("Limbo Size", ref vm.config.limboSize);
                ImGui.InputInt("Bounce Size", ref vm.config.bounceSize);
                ImGui.InputInt("Walk Size", ref vm.config.walkSize);
                ImGui.InputInt("Time Slice", ref vm.config.timeSlice);
                ImGui.LabelText("Script name", vm.scriptName);

                ImGui.LabelText("", "Variables");

                // pipe all variables thru to imgui
                for (int i = 0; i < vm.variables.Count; i++)
                {
                    ImGui.LabelText(vm.variables[i].ToString(), i.ToString());
                }

                ImGui.LabelText("", "Disassembly");

                ImGui.PushItemWidth(-1);
                ImGui.InputTextMultiline("Disassembly", ref disassembly, UInt32.MaxValue, new Vector2(-1, 250));
                ImGui.PopItemWidth();

                ImGui.End();
            }
        }
    }
}
