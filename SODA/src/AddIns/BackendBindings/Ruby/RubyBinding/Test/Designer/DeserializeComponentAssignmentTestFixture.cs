﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision: 5343 $</version>
// </file>

using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Windows.Forms;
using ICSharpCode.RubyBinding;
using IronRuby.Compiler.Ast;
using NUnit.Framework;
using RubyBinding.Tests.Utils;

namespace RubyBinding.Tests.Designer
{
	/// <summary>
	/// Tests that the string "@menuItem1" is converted to the matching component.
	/// </summary>
	[TestFixture]
	public class DeserializeComponentAssignmentTestFixture : DeserializeAssignmentTestFixtureBase
	{
		Button button;
		
		public override string GetRubyCode()
		{
			button = (Button)base.componentCreator.CreateInstance(typeof(Button), new object[0], "button1", false);
			return "self.AcceptButton = @button1";
		}
		
		[Test]
		public void DeserializedObjectIsButton()
		{
			Assert.AreSame(button, deserializedObject);
		}
	}
}
