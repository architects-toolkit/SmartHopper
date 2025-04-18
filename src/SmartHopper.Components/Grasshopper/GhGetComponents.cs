/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using SmartHopper.Components.Properties;
using SmartHopper.Core.Grasshopper;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that converts selected or all Grasshopper components to GhJSON format.
    /// Supports optional filtering by runtime messages: errors, warnings, and remarks.
    /// </summary>
    public class GhGetComponents : GH_Component
    {
        private List<string> lastComponentNames = new List<string>();
        private List<string> lastComponentGuids = new List<string>();
        private string lastJsonOutput = "";
        internal List<IGH_ActiveObject> selectedObjects = new List<IGH_ActiveObject>();
        private bool inSelectionMode = false;

        public GhGetComponents()
            : base("Get Selected Components", "GhGetSel", 
                  "Convert selected Grasshopper components to GhJSON format", 
                  "SmartHopper", "Grasshopper")
        {
        }

        public override void CreateAttributes()
        {
            m_attributes = new GhGetComponentsAttributes(this);
        }

        public override Guid ComponentGuid => new Guid("E7BB7C92-9565-584C-C1DD-425E77651FD8");

        protected override System.Drawing.Bitmap Icon => Resources.ghget;

        public void EnableSelectionMode()
        {
            // Clear previous selection
            selectedObjects.Clear();
            inSelectionMode = true;
            
            // Get the Grasshopper canvas
            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return;

            // Enable selection mode
            canvas.ContextMenuStrip?.Hide();
            Canvas_SelectionChanged();
            
            // Force component update
            ExpireSolution(true);
        }

        private void Canvas_SelectionChanged()
        {
            if (!inSelectionMode) return;

            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return;

            // Store selected objects
            selectedObjects = canvas.Document.SelectedObjects()
                .OfType<IGH_ActiveObject>()
                .ToList();
            
            // Update message with selection count
            Message = $"{selectedObjects.Count} selected";
            
            // Force component update
            ExpireSolution(true);
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => EnableSelectionMode());
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item);
            pManager.AddTextParameter("Filter", "F", "Optional filter by tags: 'error', 'warning', 'remark', 'selected', 'unselected', 'enabled', 'disabled'. Prefix '+' to include, '-' to exclude.", GH_ParamAccess.list, "");
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "List of names", GH_ParamAccess.list);
            pManager.AddTextParameter("Guids", "G", "List of guids", GH_ParamAccess.list);
            pManager.AddTextParameter("JSON", "J", "Details in JSON format", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // get input run
            object runObject = null;
            if (!DA.GetData(0, ref runObject)) return;

            if (!(runObject is GH_Boolean run))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Run must be a boolean");
                return;
            }

            if (!run.Value)
            {
                if (lastComponentNames.Count > 0)
                {
                    DA.SetDataList(0, lastComponentNames);
                    DA.SetDataList(1, lastComponentGuids);
                    DA.SetData(2, lastJsonOutput);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the component");
                }
                return;
            }

            // Clear previous results when starting a new run
            lastComponentNames.Clear();
            lastComponentGuids.Clear();
            lastJsonOutput = "";

            try
            {
                // Use selected objects if available, otherwise use all objects
                var objects = selectedObjects.Count > 0 
                    ? selectedObjects 
                    : GHCanvasUtils.GetCurrentObjects();

                // Parse include/exclude filters (+/- syntax) and normalize tags
                var filters = new List<string>();
                DA.GetDataList(1, filters);
                if (filters.Any(f => !string.IsNullOrWhiteSpace(f)))
                {
                    var includeTags = new HashSet<string>();
                    var excludeTags = new HashSet<string>();
                    foreach (var raw in filters)
                    {
                        var tok = raw.Trim();
                        if (string.IsNullOrEmpty(tok)) continue;
                        bool include = !tok.StartsWith("-");
                        var tag = tok.TrimStart('+', '-').ToLowerInvariant();
                        // normalize plural
                        if (tag.EndsWith("s")) tag = tag.Substring(0, tag.Length - 1);
                        // map locked/unlocked to enabled/disabled
                        if (tag == "locked") tag = "disabled";
                        if (tag == "unlocked") tag = "enabled";
                        if (include) includeTags.Add(tag);
                        else excludeTags.Add(tag);
                    }

                    List<IGH_ActiveObject> resultObjects;
                    // Start with included set or all
                    if (includeTags.Any())
                    {
                        resultObjects = new List<IGH_ActiveObject>();
                        // selection filters
                        if (includeTags.Contains("selected"))
                            resultObjects.AddRange(objects.Where(o => o.Attributes.Selected));
                        if (includeTags.Contains("unselected"))
                            resultObjects.AddRange(objects.Where(o => !o.Attributes.Selected));
                        // enable/disable filters
                        resultObjects.AddRange(includeTags.Contains("enabled")
                            ? objects.OfType<GH_Component>().Where(c => c.Locked).Cast<IGH_ActiveObject>()
                            : Enumerable.Empty<IGH_ActiveObject>());
                        resultObjects.AddRange(includeTags.Contains("disabled")
                            ? objects.OfType<GH_Component>().Where(c => !c.Locked).Cast<IGH_ActiveObject>()
                            : Enumerable.Empty<IGH_ActiveObject>());
                        // runtime message filters
                        if (includeTags.Contains("error"))
                            resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any()));
                        if (includeTags.Contains("warning"))
                            resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any()));
                        if (includeTags.Contains("remark"))
                            resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Remark).Any()));
                    }
                    else
                    {
                        resultObjects = new List<IGH_ActiveObject>(objects);
                    }

                    // Exclude specified tags
                    if (excludeTags.Contains("selected"))
                        resultObjects.RemoveAll(o => o.Attributes.Selected);
                    if (excludeTags.Contains("unselected"))
                        resultObjects.RemoveAll(o => !o.Attributes.Selected);
                    if (excludeTags.Contains("enabled"))
                        resultObjects.RemoveAll(o => (o is GH_Component c && c.Locked));
                    if (excludeTags.Contains("disabled"))
                        resultObjects.RemoveAll(o => (o is GH_Component c && !c.Locked));
                    if (excludeTags.Contains("error"))
                        resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any());
                    if (excludeTags.Contains("warning"))
                        resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any());
                    if (excludeTags.Contains("remark"))
                        resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Remark).Any());

                    objects = resultObjects.Distinct().ToList();
                }

                // Get the details of each object
                var document = GHDocumentUtils.GetObjectsDetails(objects);

                // Get unique component names
                var componentNames = document.Components.Select(c => c.Name).Distinct().ToList();

                // Get unique component guids
                var componentGuids = document.Components.Select(c => c.ComponentGuid.ToString()).Distinct().ToList();

                // Convert to JSON
                var json = JsonConvert.SerializeObject(document, Formatting.None);

                // Store results
                lastComponentNames = componentNames;
                lastComponentGuids = componentGuids;
                lastJsonOutput = json;

                // Set outputs
                DA.SetDataList(0, componentNames);
                DA.SetDataList(1, componentGuids);
                DA.SetData(2, json);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }

    public class GhGetComponentsAttributes : GH_ComponentAttributes
    {
        private new readonly GhGetComponents Owner;
        private Rectangle ButtonBounds;
        private bool IsHovering;
        private bool IsClicking;

        public GhGetComponentsAttributes(GhGetComponents owner) : base(owner)
        {
            Owner = owner;
            IsHovering = false;
            IsClicking = false;
        }

        protected override void Layout()
        {
            base.Layout();

            // Add space for the button at the bottom of the component
            const int margin = 5;
            var width = (int)Bounds.Width - (2 * margin);  // Subtract margins from both sides
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
                var button = ButtonBounds;

                // Draw button background with different states
                var palette = IsClicking ? GH_Palette.White : (IsHovering ? GH_Palette.Grey : GH_Palette.Black);
                var capsule = GH_Capsule.CreateCapsule(button, palette);
                capsule.Render(graphics, Selected, Owner.Locked, false);
                capsule.Dispose();

                // Draw button text
                var font = GH_FontServer.Standard;
                var text = "Select";
                var textSize = graphics.MeasureString(text, font);
                
                // Use PointF for text position
                var tx = button.X + (button.Width - textSize.Width) / 2;
                var ty = button.Y + (button.Height - textSize.Height) / 2;
                graphics.DrawString(text, font, IsHovering || IsClicking ? Brushes.Black : Brushes.White, new PointF(tx, ty));

                // Draw rectangles around selected components when hovering
                if (IsHovering && Owner.selectedObjects.Count > 0)
                {
                    using (var pen = new Pen(Color.DodgerBlue, 2f))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        foreach (var obj in Owner.selectedObjects)
                        {
                            if (obj is IGH_DocumentObject docObj)
                            {
                                // Get current bounds of the component
                                var bounds = docObj.Attributes.Bounds;
                                
                                // Add a small padding around the component
                                var padding = 4f;
                                var highlightBounds = RectangleF.Inflate(bounds, padding, padding);
                                graphics.DrawRectangle(pen, highlightBounds.X, highlightBounds.Y, 
                                                    highlightBounds.Width, highlightBounds.Height);
                            }
                        }
                    }
                }
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (ButtonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y))
                {
                    IsClicking = true;
                    Owner.ExpireSolution(true);
                    Owner.EnableSelectionMode();
                    return GH_ObjectResponse.Handled;
                }
            }
            return base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            bool wasHovering = IsHovering;
            IsHovering = ButtonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y);
            
            if (wasHovering != IsHovering)
            {
                Owner.ExpireSolution(true);
            }
            
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
