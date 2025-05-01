/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Config.Managers;
using SmartHopper.Core.Grasshopper.Utils;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that arranges selected Grasshopper objects into a tidy grid layout based on dependencies.
    /// </summary>
    public class GhTidyUpComponents : GH_Component
    {
        private List<string> lastErrors = new List<string>();
        internal List<IGH_ActiveObject> selectedObjects = new List<IGH_ActiveObject>();
        private bool inSelectionMode = false;

        public GhTidyUpComponents()
          : base("Tidy Up", "GhTidyUp",
                 "Organize selected components into a tidy grid layout",
                 "SmartHopper", "Grasshopper")
        {
        }

        public override Guid ComponentGuid => new Guid("D4C8A9E5-B123-4F67-8C90-1234567890AB");
        protected override Bitmap Icon => null;

        public void EnableSelectionMode()
        {
            selectedObjects.Clear();
            inSelectionMode = true;
            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return;
            canvas.ContextMenuStrip?.Hide();
            Canvas_SelectionChanged();
            ExpireSolution(true);
        }

        private void Canvas_SelectionChanged()
        {
            if (!inSelectionMode) return;
            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return;
            selectedObjects = canvas.Document.SelectedObjects()
                .OfType<IGH_ActiveObject>()
                .ToList();
            Message = $"{selectedObjects.Count} selected";
            ExpireSolution(true);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => EnableSelectionMode());
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Errors", "E", "List of errors during tidy up", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object runObj = null;
            if (!DA.GetData(0, ref runObj)) return;
            if (!(runObj is GH_Boolean run))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Run must be a boolean");
                return;
            }
            if (!run.Value)
            {
                if (lastErrors.Count > 0)
                    DA.SetDataList(0, lastErrors);
                else
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute tidy up");
                return;
            }
            lastErrors.Clear();
            var guids = selectedObjects.Select(o => o.InstanceGuid.ToString()).ToList();
            if (!guids.Any())
            {
                lastErrors.Add("No components selected");
                DA.SetDataList(0, lastErrors);
                return;
            }
            try
            {
                var parameters = new JObject { ["guids"] = JArray.FromObject(guids) };
                var result = AIToolManager.ExecuteTool("ghtidyup", parameters, null)
                                  .GetAwaiter().GetResult() as JObject;
                if (result == null)
                    lastErrors.Add("Tool 'ghtidyup' returned invalid result");
                else if (result["success"]?.ToObject<bool>() == false)
                    lastErrors.Add(result["error"]?.ToString() ?? "Unknown error");
                else
                {
                    var moved = result["moved"]?.ToObject<List<string>>() ?? new List<string>();
                    var failed = guids.Except(moved);
                    foreach (var g in failed)
                        lastErrors.Add($"Component {g} not moved");
                }
            }
            catch (Exception ex)
            {
                lastErrors.Add(ex.Message);
            }
            DA.SetDataList(0, lastErrors);
        }

        public override void CreateAttributes()
        {
            m_attributes = new GhTidyUpComponentsAttributes(this);
        }
    }

    /// <summary>
    /// Custom attributes to support selection button for GhTidyUpComponents.
    /// </summary>
    public class GhTidyUpComponentsAttributes : GH_ComponentAttributes
    {
        private new readonly GhTidyUpComponents Owner;
        private Rectangle ButtonBounds;
        private bool IsHovering;
        private bool IsClicking;

        public GhTidyUpComponentsAttributes(GhTidyUpComponents owner) : base(owner)
        {
            Owner = owner;
            IsHovering = false;
            IsClicking = false;
        }

        protected override void Layout()
        {
            base.Layout();
            const int margin = 5;
            var width = (int)Bounds.Width - (2 * margin);
            var height = 24;
            var x = (int)Bounds.X + margin;
            var y = (int)Bounds.Bottom;
            ButtonBounds = new Rectangle(x, y, width, height);
            Bounds = new RectangleF(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height + height + margin);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel == GH_CanvasChannel.Objects)
            {
                var palette = IsClicking ? GH_Palette.White : (IsHovering ? GH_Palette.Grey : GH_Palette.Black);
                var capsule = GH_Capsule.CreateCapsule(ButtonBounds, palette);
                capsule.Render(graphics, Selected, Owner.Locked, false);
                capsule.Dispose();
                var font = GH_FontServer.Standard;
                var text = "Select";
                var size = graphics.MeasureString(text, font);
                var tx = ButtonBounds.X + (ButtonBounds.Width - size.Width) / 2;
                var ty = ButtonBounds.Y + (ButtonBounds.Height - size.Height) / 2;
                graphics.DrawString(text, font, (IsHovering || IsClicking) ? Brushes.Black : Brushes.White, new PointF(tx, ty));
                if (IsHovering && Owner.selectedObjects.Count > 0)
                {
                    using (var pen = new Pen(Color.DodgerBlue, 2f))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        foreach (var obj in Owner.selectedObjects.OfType<IGH_DocumentObject>())
                        {
                            var b = GHComponentUtils.GetComponentBounds(obj.InstanceGuid);
                            var pad = 4f;
                            var hb = RectangleF.Inflate(b, pad, pad);
                            graphics.DrawRectangle(pen, hb.X, hb.Y, hb.Width, hb.Height);
                        }
                    }
                }
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && ButtonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y))
            {
                IsClicking = true;
                Owner.ExpireSolution(true);
                Owner.EnableSelectionMode();
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var was = IsHovering;
            IsHovering = ButtonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y);
            if (was != IsHovering) Owner.ExpireSolution(true);
            return base.RespondToMouseMove(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (IsClicking)
            {
                IsClicking = false;
                Owner.ExpireSolution(true);
            }
            return base.RespondToMouseUp(sender, e);
        }
    }
}
