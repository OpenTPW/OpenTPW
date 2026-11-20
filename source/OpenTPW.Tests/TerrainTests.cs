using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class TerrainTests
{
	[TestMethod]
	public void EveryLevelHasAHeightfield()
	{
		GameData.Require();

		// Every level folder with a terrain archive (archives are listed as folders)
		var levels = FileSystem.GetDirectories( "levels" ).Select( x => System.IO.Path.GetFileName( x ) )
			.Where( x => FileSystem.GetDirectories( $"levels/{x}" ).Any( d => System.IO.Path.GetFileName( d ).Equals( "terrain", StringComparison.OrdinalIgnoreCase ) ) ).ToList();
		Assert.IsTrue( levels.Count > 0 );

		foreach ( var level in levels )
		{
			var model = new ModelFile( FileSystem.OpenRead( $"levels/{level}/terrain/base.md2" ) );
			var heightfield = model.Heightfield;

			Assert.IsNotNull( heightfield, level );
			Assert.AreEqual( (heightfield.Width + 1) * (heightfield.Depth + 1), heightfield.Heights.Length, level );
			Assert.AreEqual( heightfield.Width * heightfield.Depth, heightfield.Cells.Length, level );

			for ( int z = 0; z < heightfield.Depth; z++ )
				for ( int x = 0; x < heightfield.Width; x++ )
					if ( !heightfield.IsWater( x, z ) )
						Assert.IsTrue( heightfield.TextureAt( x, z ) < model.TextureNames.Length, $"{level} ({x}, {z})" );
		}
	}
}
