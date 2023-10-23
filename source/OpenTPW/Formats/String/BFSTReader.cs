﻿using OpenTPW.Formats.String;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veldrid.MetalBindings;
using Vulkan;

namespace OpenTPW;
public sealed class BFSTReader : BaseFormat
{
	private BFSTStream memoryStream;
	private BFMUReader mtuReader = new BFMUReader( $"{Settings.Default.GamePath}\\data\\Language\\English\\MBToUni.dat" );
	public byte[] buffer;


	public BFSTReader (string path )
	{
		using var fileStream = File.OpenRead( path );
		ReadFromStream( fileStream );
	}

	public BFSTReader (Stream stream )
	{
		ReadFromStream(stream);
	}
	public void Dispose()
	{
		memoryStream.Dispose();
	}

	protected override void ReadFromStream(Stream stream)
	{
		// Set up read buffer
		var tempStreamReader = new StreamReader( stream );
		var fileLength = (int)tempStreamReader.BaseStream.Length;

		buffer = new byte[fileLength];
		tempStreamReader.BaseStream.Read( buffer, 0, fileLength );
		tempStreamReader.Close();

		memoryStream = new BFSTStream( buffer );

		ReadFile();
	}

	public string[] ReadFile()
	{
		memoryStream.Seek( 0, SeekOrigin.Begin );

		/*	
		Header
			4 bytes: Magic number - "BFST"
			4 bytes: Unknown
			4 bytes: String count
			
		#For each string
			4 bytes - String offset (from the end of the string count)
			
		#For each string (at offset)
			1 byte - Unknown, always 0x01
			3 bytes - String length
			n bytes - Each character, specified with an offset in a BFMU file.
			4 bytes - Padding (may be longer?)
		*/

		var magicNumber = memoryStream.ReadString( 4 );

		if ( magicNumber != "BFST" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		//Unknown
		_ = memoryStream.ReadInt32();


		//String Count
		var stringCount = memoryStream.ReadInt32();
		
		//Save Pos and create
		var initialMemPos = memoryStream.Position;
		List<string> outputList = new List<string>();

		for ( int i = 0; i < stringCount; i++ )
		{
			// Go to next offset based on iteration
			memoryStream.Seek( 4 * (i), SeekOrigin.Current );

			// Find offset number
			int offset = memoryStream.ReadInt32();

			// go to offset address
			// need to account offset for inital 12 bytes
			memoryStream.Seek( offset + 12 , SeekOrigin.Begin );

			// Unknown - we still want to verify it is 0x1
			var unknownOne = memoryStream.ReadByte();
			if(unknownOne != 1 )
			{
				throw new Exception( $"String Offset not working - offset set to {unknownOne} @ Pos:{memoryStream.Position}" );
			}

			// String Length
			var stringLength = memoryStream.ReadByte();

			//unused after string length
			_ = memoryStream.ReadByte();
			_ = memoryStream.ReadByte();

			StringBuilder str = new StringBuilder();
			// Characters
			for ( int j = 0; j < stringLength; j++ )
			{
				var mtuPos = memoryStream.ReadByte();
				var readCharacter = mtuReader.GetCharacter( mtuPos );
				str.Append( readCharacter );
			}
			
			outputList.Add( str.ToString() );

			// Go back to intial position
			memoryStream.Seek( initialMemPos, SeekOrigin.Begin );
			
		}

		return outputList.ToArray();
	}

}
