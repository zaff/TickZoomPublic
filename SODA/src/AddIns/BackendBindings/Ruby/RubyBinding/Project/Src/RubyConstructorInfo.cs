﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision: 5343 $</version>
// </file>

using System;
using System.Collections.Generic;
using ICSharpCode.NRefactory.Ast;

namespace ICSharpCode.RubyBinding
{
	public class RubyConstructorInfo
	{
		ConstructorDeclaration constructor;
		List<FieldDeclaration> fields = new List<FieldDeclaration>();
		
		RubyConstructorInfo(ConstructorDeclaration constructor, List<FieldDeclaration> fields)
		{
			this.constructor = constructor;
			this.fields = fields;
		}
		
		/// <summary>
		/// Gets the constructor information from a type declaration. Returns null if there is no 
		/// constructor defined or if there are no fields defined.
		/// </summary>
		public static RubyConstructorInfo GetConstructorInfo(TypeDeclaration type)
		{
			List<FieldDeclaration> fields = new List<FieldDeclaration>();
			ConstructorDeclaration constructor = null;
			foreach (INode node in type.Children) {
				ConstructorDeclaration currentConstructor = node as ConstructorDeclaration;
				FieldDeclaration field = node as FieldDeclaration;
				if (currentConstructor != null) {
					constructor = currentConstructor;
				} else if (field != null) {
					fields.Add(field);
				}
			}
			
			if ((fields.Count > 0) || (constructor != null)) {
				return new RubyConstructorInfo(constructor, fields);
			}
			return null;
		}
		
		public ConstructorDeclaration Constructor {
			get { return constructor; }
		}
		
		public List<FieldDeclaration> Fields {
			get { return fields; }
		}
	}
}
