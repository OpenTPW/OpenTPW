using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class StartupPathTests
{
	[TestMethod]
	public void LauncherDirectoryDoesNotHidePackagedOrDevelopmentContent()
	{
		string root = Path.Join( Path.GetTempPath(), "opentpw-startup-" + Guid.NewGuid().ToString( "N" ) );
		try
		{
			string launcher = Path.Join( root, "launcher" ), project = Path.Join( root, "project" );
			string executable = Path.Join( project, "source", "bin" );
			Directory.CreateDirectory( launcher ); Directory.CreateDirectory( executable );
			void Content( string directory )
			{
				Directory.CreateDirectory( Path.Join( directory, "content", "shaders" ) );
				File.WriteAllText( Path.Join( directory, "content", "shaders", "ui.shader" ), "fixture" );
			}
			Assert.ThrowsException<DirectoryNotFoundException>( () => EngineContent.Find( launcher, executable ) );
			Content( project );
			Assert.AreEqual( project, EngineContent.Find( launcher, executable ) );
			Content( executable );
			Assert.AreEqual( executable, EngineContent.Find( launcher, executable ) );
			Assert.AreEqual( project, EngineContent.Find( project, executable ), "An explicit development working directory takes precedence" );
			Directory.CreateDirectory( Path.Join( root, "tpwroot", "data", "Language" ) );
			Assert.AreEqual( Path.Join( root, "tpwroot" ), GameDir.Find( executable ) );
		}
		finally { if ( Directory.Exists( root ) ) Directory.Delete( root, true ); }
	}
}
