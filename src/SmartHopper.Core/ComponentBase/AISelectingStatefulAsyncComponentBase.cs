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
using Timer = System.Timers.Timer;

namespace SmartHopper.Core.ComponentBase
{
    public abstract class AISelectingStatefulAsyncComponentBase : AIStatefulAsyncComponentBase, ISelectingComponent
    {
        public List<IGH_ActiveObject> SelectedObjects { get; private set; } = new List<IGH_ActiveObject>();

        private readonly SelectingComponentCore selectionCore;

        protected AISelectingStatefulAsyncComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            this.selectionCore = new SelectingComponentCore(this, this);
            Instances.DocumentServer.DocumentAdded += this.OnDocumentAdded;
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            Instances.DocumentServer.DocumentAdded -= this.OnDocumentAdded;
        }

        public override void CreateAttributes()
        {
            this.m_attributes = new SelectingComponentAttributes(this, this);
        }

        public void EnableSelectionMode()
        {
            this.selectionCore.EnableSelectionMode();
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            this.Menu_AppendItem(menu, "Select Components", (s, e) => this.EnableSelectionMode());
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (!base.Write(writer))
            {
                return false;
            }

            return this.selectionCore.Write(writer);
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader))
            {
                return false;
            }

            return this.selectionCore.Read(reader);
        }

        private void OnDocumentAdded(GH_DocumentServer sender, GH_Document doc)
        {
            this.selectionCore.OnDocumentAdded(doc);
        }
    }
}
