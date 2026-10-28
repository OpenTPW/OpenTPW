using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class SettingsTests
{
	[TestMethod]
	public void SettingsLiveInTheExecutableFolderAndRoundTrip()
	{
		Assert.AreEqual( Path.Combine( AppContext.BaseDirectory, Settings.FileName ), Settings.Default.FilePath );

		string root = Path.Combine( Path.GetTempPath(), "opentpw-settings-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( root );
		try
		{
			string path = Path.Combine( root, Settings.FileName );
			var settings = Settings.Load( path );
			Assert.IsFalse( File.Exists( path ) );
			Assert.AreEqual( "", settings.LanguagePack );
			Assert.AreEqual( new Point( 1280, 720 ), settings.GameWindowSize );

			settings.LanguagePack = @"D:\Games\TPW_tw";
			settings.Language = "TraditionalChinese";
			settings.GameWindowSize = new Point( 800, 600 );
			settings.Save();

			var reloaded = Settings.Load( path );
			Assert.AreEqual( @"D:\Games\TPW_tw", reloaded.LanguagePack );
			Assert.AreEqual( "TraditionalChinese", reloaded.Language );
			Assert.AreEqual( new Point( 800, 600 ), reloaded.GameWindowSize );
			Assert.AreEqual( "med", reloaded.LobbyQuality );
			Assert.IsFalse( File.Exists( path + ".tmp" ) );
		}
		finally
		{
			Directory.Delete( root, true );
		}
	}

	[TestMethod]
	public void MalformedSettingsFail()
	{
		string root = Path.Combine( Path.GetTempPath(), "opentpw-settings-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( root );
		try
		{
			string path = Path.Combine( root, Settings.FileName );
			foreach ( var text in new[] { "{ \"Langauge\": \"German\" }", "{ \"GameWindowSize\": [800] }", "{ \"Language\": null }", "{" } )
			{
				File.WriteAllText( path, text );
				Assert.ThrowsException<InvalidDataException>( () => Settings.Load( path ), text );
			}
		}
		finally
		{
			Directory.Delete( root, true );
		}
	}
}
