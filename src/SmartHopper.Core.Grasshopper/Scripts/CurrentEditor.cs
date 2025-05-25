/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Portions of this code adapted from:
 * https://github.com/4kk11/GHScriptGPT
 * MIT License
 * Copyright (c) 2023 Akihito Yokota
 */

using Grasshopper.GUI.Script;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SmartHopper.Core.Grasshopper.Scripts
{
	public class CurrentEditor
	{
		private GH_ScriptEditor _editor;
		private GH_CodeBlocks _codeBlocks;
		private CurrentEditor(GH_ScriptEditor editor)
		{
			_editor = editor;
			_codeBlocks = editor.GetSourceCode();
		}

		public static CurrentEditor GetCurrentEditor()
		{
			//Check if only one editor is open
			var forms = System.Windows.Forms.Application.OpenForms;
			GH_ScriptEditor validEditor = null;
			
			foreach (var form in forms)
			{
				if (form.GetType().Name != "GH_ScriptEditor") continue;
				if (validEditor != null) // Already found one, therefore more than one exists
				{
					return null; // return null if more than one "GH_ScriptEditor" is found
				}
				validEditor = form as GH_ScriptEditor; // Assign the found form to validEditor
			}

			// If validEditor was assigned, return a new CurrentEditor instance. Otherwise, return null.
			return validEditor != null ? new CurrentEditor(validEditor) : null;
		}

		public SourceCode GetCode_RunScript()
		{
			// Get the code of the RunScript function.
			var lines = _codeBlocks[2].Lines;
			lines = lines.Skip(lines.Count() - 2);
			lines = lines.Concat(_codeBlocks[3].Lines);
			lines = lines.Append("}");
			lines = lines.Select(text => text.TrimStart());
			return new SourceCode(lines);
		}

		public void SetCode_RunScript(SourceCode code)
		{
			GH_CodeBlock newCodeBlock = new GH_CodeBlock();
			newCodeBlock.AddLines(code.CodeLines);
			_codeBlocks[3] = newCodeBlock;
			UpdateEditor();
		}

		private void UpdateEditor()
		{
			_editor.SetSourceCode(_codeBlocks);
		}

		public IEnumerable<string> GetErrors()
		{
			IGH_DocumentObject owner = GetOwner();

			var fieldInfo = owner.GetType().BaseType.GetField("_compilerErrors", BindingFlags.Instance | BindingFlags.NonPublic);
			var errors = fieldInfo.GetValue(owner) as IEnumerable<string>;
			return errors;
		}

		public void RunScript()
		{
			IGH_DocumentObject owner = GetOwner();
			_editor.CacheCurrentScript();
			var methodInfo = owner.GetType().BaseType.GetMethod("SourceCodeChanged", BindingFlags.Instance | BindingFlags.Public);
			methodInfo.Invoke(owner, new object[] {_editor});
		}

		private IGH_DocumentObject GetOwner()
		{
			FieldInfo fieldInfo = _editor.GetType().GetField("m_objectId", BindingFlags.Instance | BindingFlags.NonPublic);
			Guid instanceGuid = (Guid)fieldInfo.GetValue(_editor);

			fieldInfo = _editor.GetType().GetField("m_document", BindingFlags.Instance | BindingFlags.NonPublic);
			GH_Document ghdoc = fieldInfo.GetValue(_editor) as GH_Document;

			IGH_DocumentObject docObject = ghdoc.FindObject(instanceGuid, true);
			return docObject;
		}
	}
}
