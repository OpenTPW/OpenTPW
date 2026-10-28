using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenTPW.Tests;

[TestClass]
public class ModelTests
{
	private static byte[]? Header( string path )
	{
		using var stream = FileSystem.OpenRead( path );
		var header = new byte[0x9C];
		return stream.Read( header, 0, header.Length ) == header.Length ? header : null;
	}

	[TestMethod]
	public void EveryModelParses()
	{
		GameData.Require();

		var failures = new List<string>();
		int count = 0;

		foreach ( var path in GameData.FindFiles( ".md2" ) )
		{
			// Only version 0xDD; a few files use older layouts (0x18, 0xCF)
			var header = Header( path );
			if ( header == null || BitConverter.ToUInt32( header, 4 ) != 0xDD )
				continue;

			count++;
			try
			{
				var model = new ModelFile( FileSystem.OpenRead( path ) );
				if ( BitConverter.ToUInt32( header, 0x70 ) != 0 && model.Nodes.Count != BitConverter.ToUInt16( header, 0x42 ) )
					failures.Add( $"{path}: {model.Nodes.Count} nodes" );
			}
			catch ( Exception e )
			{
				failures.Add( $"{path}: {e.Message}" );
			}
		}

		Console.WriteLine( $"{count} models" );
		Assert.IsTrue( count > 0 );
		Assert.AreEqual( 0, failures.Count, string.Join( "\n", failures ) );
	}

	[TestMethod]
	public void EveryAnimationEvaluates()
	{
		GameData.Require();

		var failures = new List<string>();
		int count = 0;

		var models = GameData.FindFiles( ".md2" );
		var byName = models.GroupBy( x => Path.Combine( Path.GetDirectoryName( x ) ?? "", Path.GetFileNameWithoutExtension( x ) ).ToLowerInvariant() )
			.ToDictionary( x => x.Key, x => x.First() );

		foreach ( var path in models )
		{
			var header = Header( path );
			if ( header == null || BitConverter.ToUInt32( header, 4 ) != 0xDD || BitConverter.ToUInt32( header, 0x98 ) == 0 )
				continue;

			// "<model><slot letter>[n].md2" animates "<model>.md2" in the same folder
			var match = Regex.Match( Path.GetFileNameWithoutExtension( path ), @"^(.+)[A-Za-z]\d{0,2}$" );
			if ( !match.Success || !byName.TryGetValue( Path.Combine( Path.GetDirectoryName( path ) ?? "", match.Groups[1].Value ).ToLowerInvariant(), out var basePath ) )
				continue;

			count++;
			try
			{
				var model = new ModelFile( FileSystem.OpenRead( basePath ) );
				var animation = new ModelAnimation( FileSystem.OpenRead( path ) );

				for ( int i = 0; i <= 8; i++ )
				{
					var pose = animation.Evaluate( model, animation.StartFrame + animation.Length * i / 8 + 0.5f );
					if ( pose.WorldTransforms.Any( m => float.IsNaN( m.M11 ) || float.IsNaN( m.M41 ) ) ||
						pose.Positions.Any( p => p != null && p.Any( v => float.IsNaN( v.X ) ) ) )
					{
						failures.Add( $"{path}: NaN at frame {i}/8" );
						break;
					}
				}
			}
			catch ( Exception e )
			{
				failures.Add( $"{path}: {e.Message}" );
			}
		}

		Console.WriteLine( $"{count} animations" );
		Assert.IsTrue( count > 0 );
		Assert.AreEqual( 0, failures.Count, string.Join( "\n", failures ) );
	}
}
