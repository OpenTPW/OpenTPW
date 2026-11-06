using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>Loads every game shader using its production SDL resource-count declarations.</summary>
[TestClass]
[DoNotParallelize]
public class ShaderTests
{
	[TestMethod]
	public void EveryPrecompiledGameProgramLoads()
	{
		using var device = OpenTPW.Graphics.GpuDevice.Create();
		Global.Device = device;
		try
		{
			foreach ( string name in new[] { "3d", "water", "lobby-model", "test", "unlit", "lobby-sky", "blit", "ui", "ui-lit", "lobby-particles", "lobby-weather" } )
			{
				var shader = new Shader( Content.GetPath( $"shaders/{name}.shader" ) );
				Assert.AreEqual( name, shader.Program.Name );
			}
		}
		finally { Shader.ReleasePrograms(); }
	}
}
