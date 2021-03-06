﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision: 4747 $</version>
// </file>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using ICSharpCode.PythonBinding;
using NUnit.Framework;
using PythonBinding.Tests.Utils;

namespace PythonBinding.Tests.Designer
{
	[TestFixture]
	public class LoadUserControlWithDoublePropertyTestFixture
	{	
		MockComponentCreator componentCreator = new MockComponentCreator();
		DoublePropertyUserControl userControl;
		Form form;
		
		public string PythonCode {
			get {
				Type type = typeof(DoublePropertyUserControl);
				componentCreator.AddType(type.FullName, type);
				return "class MainForm(System.Windows.Forms.Form):\r\n" +
							"    def InitializeComponent(self):\r\n" +
							"        self._userControl = PythonBinding.Tests.Utils.DoublePropertyUserControl()\r\n" +
							"        self.SuspendLayout()\r\n" +
							"        # \r\n" +
							"        # userControl1\r\n" +
							"        # \r\n" +
							"        self._userControl.DoubleValue = 0\r\n" +
							"        # \r\n" +
							"        # MainForm\r\n" +
							"        # \r\n" +
							"        self.ClientSize = System.Drawing.Size(300, 400)\r\n" +
							"        self.Controls.Add(self._userControl)\r\n" +
							"        self.Name = \"MainForm\"\r\n" +
							"        self.ResumeLayout(False)\r\n";
			}
		}

		[TestFixtureSetUp]
		public void SetUpFixture()
		{			
			PythonComponentWalker walker = new PythonComponentWalker(componentCreator);
			form = walker.CreateComponent(PythonCode) as Form;
			userControl = form.Controls[0] as DoublePropertyUserControl;
		}

		[TestFixtureTearDown]
		public void TearDownFixture()
		{
			form.Dispose();
		}
		
		[Test]
		public void UserControlDoubleProperty()
		{
			Assert.AreEqual(0, userControl.DoubleValue);
		}		
	}
}
