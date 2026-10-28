using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyAnimationTests
{
	[TestMethod]
	public void EveryLobbyAnimationSlotEvaluatesWithItsBaseModel()
	{
		GameData.Require();
		var found = new List<string>();
		var pending = new Stack<string>();
		pending.Push( "lobby" );
		while ( pending.Count > 0 )
		{
			var directory = pending.Pop();
			found.AddRange( FileSystem.GetFiles( directory ).Where( p => p.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) ) );
			foreach ( var child in FileSystem.GetDirectories( directory ) )
				pending.Push( child );
		}
		var paths = found.ToArray();
		var byName = paths.ToDictionary( p => p.Replace( '\\', '/' ).ToLowerInvariant() );
		int animations = 0, vertexTracks = 0, uvTracks = 0, transformTracks = 0;
		foreach ( var path in paths )
		{
			var match = Regex.Match( Path.GetFileNameWithoutExtension( path ), @"^(.+)[A-Za-z]\d{0,2}$" );
			if ( !match.Success )
				continue;
			string baseName = $"{Path.GetDirectoryName( path )}/{match.Groups[1].Value}.md2".Replace( '\\', '/' ).ToLowerInvariant();
			if ( !byName.TryGetValue( baseName, out var basePath ) )
				continue;

			using var modelStream = FileSystem.OpenRead( basePath );
			using var animationStream = FileSystem.OpenRead( path );
			var model = new ModelFile( modelStream );
			var animation = new ModelAnimation( animationStream );
			if ( animation.Tracks.Count == 0 )
				continue;
			animations++;
			vertexTracks += animation.Tracks.Count( t => t.PackedVertices != null || t.VertexFrames != null );
			uvTracks += animation.Tracks.Count( t => t.TexCoords != null );
			transformTracks += animation.Tracks.Count( t => t.Path != null || t.Rotations.Length > 0 );
			for ( int sample = 0; sample <= 8; sample++ )
			{
				var pose = animation.Evaluate( model, animation.StartFrame + animation.Length * sample / 8 );
				Assert.AreEqual( model.Nodes.Count, pose.WorldTransforms.Length, path );
				Assert.IsFalse( pose.WorldTransforms.Any( m => !float.IsFinite( m.M11 ) || !float.IsFinite( m.M41 ) || !float.IsFinite( m.M42 ) ), path );
				Assert.IsFalse( pose.Positions.Any( p => p != null && p.Any( v => !float.IsFinite( v.X ) || !float.IsFinite( v.Y ) || !float.IsFinite( v.Z ) ) ), path );
				Assert.IsFalse( pose.TexCoords.Any( p => p != null && p.Any( v => !float.IsFinite( v.X ) || !float.IsFinite( v.Y ) ) ), path );
			}
		}
		Console.WriteLine( $"{animations} lobby animations; {vertexTracks} vertex, {uvTracks} UV, {transformTracks} transform tracks" );
		Assert.IsTrue( animations >= 12, "Expected islands, gates and flying meshes" );
		Assert.IsTrue( vertexTracks > 0 );
		Assert.IsTrue( uvTracks > 0 );
		Assert.IsTrue( transformTracks > 0 );
	}

	/// <summary>
	/// Every model and animation that lobby.txt and the island files lead to resolves by the retail
	/// naming rule and loads; one missing file fails the test.
	/// </summary>
	[TestMethod]
	public void EveryConfiguredLobbyResourceResolvesAndLoads()
	{
		GameData.Require();
		var config = LobbyConfig.Load();
		var resolved = new List<string>();
		var loaded = new Dictionary<string, ModelFile>( StringComparer.OrdinalIgnoreCase );

		ModelFile Model( string path )
		{
			string file = AnimationFiles.ResolveModel( path );
			if ( !loaded.TryGetValue( file, out var model ) )
			{
				using var stream = FileSystem.OpenRead( file );
				loaded[file] = model = new ModelFile( stream );
				resolved.Add( file );
			}
			return model;
		}

		void Animation( ModelFile model, string file )
		{
			using var stream = FileSystem.OpenRead( file );
			var animation = new ModelAnimation( stream );
			Assert.IsTrue( animation.Length >= 0, file );
			var pose = animation.Evaluate( model, animation.StartFrame + animation.Length / 2 );
			Assert.AreEqual( model.Nodes.Count, pose.WorldTransforms.Length, file );
			resolved.Add( file );
		}

		Assert.AreEqual( 4, config.Islands.Count );
		int flocks = 0;
		foreach ( var island in config.Islands )
		{
			var islandModel = Model( island.ModelPath );
			var islandSegments = AnimationFiles.Segments( island.ModelPath, AnimationSlot.M );
			Assert.IsTrue( islandSegments.Count > 0, island.ModelPath );
			foreach ( var file in islandSegments )
				Animation( islandModel, file );

			// Inventory all gate segments, including the third segment needed by navigation.
			var gateModel = Model( island.GateModelPath );
			for ( int segment = 0; segment < 3; segment++ )
				Animation( gateModel, AnimationFiles.Resolve( island.GateModelPath, AnimationSlot.M, segment ) );

			foreach ( var mesh in island.FlyingMeshes )
			{
				// FUN_005d4900 plays slot M segment 0 looped.
				Animation( Model( mesh.ModelPath ), AnimationFiles.Resolve( mesh.ModelPath, AnimationSlot.M, 0 ) );
				flocks++;
			}
		}

		Console.WriteLine( $"{resolved.Count} lobby resources resolved:" );
		foreach ( var file in resolved )
			Console.WriteLine( $"  {file}" );
		Assert.AreEqual( 3, flocks, "Jungle pink and yellow butterflies, Halloween bats" );
	}

	[TestMethod]
	public void SegmentZeroIsTheFirstNumberedFileInStoredCase()
	{
		GameData.Require();
		var butterfly = LobbyConfig.Load().Islands.SelectMany( x => x.FlyingMeshes )
			.First( x => x.ModelName.Equals( "bfly_PINK", StringComparison.OrdinalIgnoreCase ) );
		string file = AnimationFiles.Resolve( butterfly.ModelPath, AnimationSlot.M, 0 );
		// lobby.wad stores "Bfly_PINKm1.MD2"; the old code asked for "bfly_PINKM.md2" and found nothing.
		StringAssert.EndsWith( file, "/Bfly_PINKm1.MD2" );
		Assert.AreEqual( 'M', AnimationFiles.Letter( AnimationSlot.M ) );
		Assert.AreEqual( "CDILSMEUWBRO", string.Concat( Enum.GetValues<AnimationSlot>().Select( AnimationFiles.Letter ) ) );
	}

	[TestMethod]
	public void MissingResourcesThrowWithTheFullPath()
	{
		GameData.Require();
		var island = LobbyConfig.Load().Islands[0];
		var count = AnimationFiles.Segments( island.ModelPath, AnimationSlot.M ).Count;
		var missingSegment = Assert.ThrowsException<FileNotFoundException>(
			() => AnimationFiles.Resolve( island.ModelPath, AnimationSlot.M, count ) );
		StringAssert.Contains( missingSegment.Message, FileSystem.GetAbsolutePath( "lobby" ) );
		Assert.AreEqual( 0, AnimationFiles.Segments( island.ModelPath, AnimationSlot.O ).Count );
		var missingModel = Assert.ThrowsException<FileNotFoundException>(
			() => AnimationFiles.ResolveModel( $"{island.ResourceDirectory}/no_such_model.md2" ) );
		StringAssert.Contains( missingModel.Message, "no_such_model.md2" );
	}

	[TestMethod]
	public void NumberedSegmentsTakePrecedenceAndStopAtTheFirstGap()
	{
		WithLooseFiles( new[] { "actor.md2", "actorM.md2", "actorM1.md2", "actorM2.md2", "actorM4.md2" }, () =>
		{
			CollectionAssert.AreEqual( new[] { "actorM1.md2", "actorM2.md2" },
				AnimationFiles.Segments( "actor.md2", AnimationSlot.M ).ToArray() );
			Assert.ThrowsException<FileNotFoundException>( () => AnimationFiles.Resolve( "actor.md2", AnimationSlot.M, 2 ) );
		} );
	}

	[TestMethod]
	public void UnnumberedFallbackIsOnlyUsedWhenSegmentOneIsMissing()
	{
		WithLooseFiles( new[] { "actor.md2", "actorM.md2", "actorM2.md2" }, () =>
		{
			Assert.AreEqual( "actorM.md2", AnimationFiles.Resolve( "actor.md2", AnimationSlot.M, 0 ) );
			Assert.AreEqual( 1, AnimationFiles.Segments( "actor.md2", AnimationSlot.M ).Count );
			Assert.ThrowsException<ArgumentOutOfRangeException>( () => AnimationFiles.Resolve( "actor.md2", AnimationSlot.M, -1 ) );
			Assert.ThrowsException<ArgumentOutOfRangeException>( () => AnimationFiles.Letter( (AnimationSlot)12 ) );
			Assert.ThrowsException<FileNotFoundException>( () => AnimationFiles.Segments( "missing.md2", AnimationSlot.M ) );
		} );
	}

	[TestMethod]
	public void EveryConfiguredFlyingMeshChangesPose()
	{
		GameData.Require();
		foreach ( var mesh in LobbyConfig.Load().Islands.SelectMany( x => x.FlyingMeshes ) )
		{
			var model = new ModelFile( AnimationFiles.ResolveModel( mesh.ModelPath ) );
			var animation = new ModelAnimation( AnimationFiles.Resolve( mesh.ModelPath, AnimationSlot.M, 0 ) );
			var first = animation.Evaluate( model, animation.StartFrame );
			bool changed = false;
			for ( int sample = 1; sample <= 8; sample++ )
			{
				var pose = animation.Evaluate( model, animation.StartFrame + animation.Length * sample / 8f );
				changed |= !first.WorldTransforms.SequenceEqual( pose.WorldTransforms );
				for ( int part = 0; part < pose.Positions.Length; part++ )
					if ( first.Positions[part] is { } a && pose.Positions[part] is { } b ) changed |= !a.SequenceEqual( b );
			}
			Assert.IsTrue( changed, $"Flying mesh never changes pose: {mesh.ModelPath}" );
		}
	}

	private static void WithLooseFiles( string[] names, Action test )
	{
		var previous = FileSystem;
		string root = Path.Combine( Path.GetTempPath(), $"OpenTPW-animation-files-{Guid.NewGuid():N}" );
		Directory.CreateDirectory( root );
		try
		{
			foreach ( var name in names ) File.WriteAllBytes( Path.Combine( root, name ), Array.Empty<byte>() );
			FileSystem = new BaseFileSystem( root );
			test();
		}
		finally
		{
			FileSystem = previous;
			foreach ( var name in names ) File.Delete( Path.Combine( root, name ) );
			Directory.Delete( root );
		}
	}
}
