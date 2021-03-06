// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision: 2043 $</version>
// </file>

using ICSharpCode.SharpDevelop;
using SD = ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Project;
using NUnit.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Services.Description;
using System.Web.Services.Discovery;

namespace ICSharpCode.SharpDevelop.Tests.WebReferences
{
	/// <summary>
	/// Tests that an exception is thrown if an unsupported project language
	/// is used with the web reference
	/// </summary>
	[TestFixture]
	public class UnsupportedLanguageTest
	{
		SD.WebReference webReference;
		DiscoveryClientProtocol protocol;
		FileProjectItem proxyFileProjectItem;
		MSBuildBasedProject project;
		
		string name = "localhost";
		string proxyNamespace = "WebReferenceNamespace";
		string updateFromUrl = "http://localhost/test.asmx";
		
		[Test]
		[ExpectedException(typeof(NotSupportedException))]
		public void NotSupportedProjectLanguage()
		{
			project = WebReferenceTestHelper.CreateTestProject("Foo");

			protocol = new DiscoveryClientProtocol();
			DiscoveryDocumentReference discoveryRef = new DiscoveryDocumentReference();
			discoveryRef.Url = updateFromUrl;
			protocol.References.Add(discoveryRef);
			
			ContractReference contractRef = new ContractReference();
			contractRef.Url = "http://localhost/test.asmx?wsdl";
			contractRef.ClientProtocol = new DiscoveryClientProtocol();
			ServiceDescription desc = new ServiceDescription();
			contractRef.ClientProtocol.Documents.Add(contractRef.Url, desc);
			protocol.References.Add(contractRef);
			
			WebReferenceTestHelper.InitializeLanguageBindings();
			
			webReference = new SD.WebReference(project, updateFromUrl, name, proxyNamespace, protocol);
			
			proxyFileProjectItem = WebReferenceTestHelper.GetFileProjectItem(webReference.Items, "Web References\\localhost\\Reference.vb", ItemType.Compile);
		}
	}
}
