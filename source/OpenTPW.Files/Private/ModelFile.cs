using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text;

namespace OpenTPW;

// Theme Park World MD2 (version 0xDD), not Quake II MD2. Offsets in the file
// are absolute from byte zero. This reader leaves them as offsets; the game
// loader itself overwrites them with in-memory pointers.
public partial class ModelFile : BaseFormat
{
	private const uint Magic = 0x1CD15D46;
	private const int HeaderSize = 0xB0;
	private const int MeshRecordSize = 0xA0;
	private byte[] data = Array.Empty<byte>();

	public List<MeshData> MeshDataList { get; private set; } = new();
	public List<NodeData> Nodes { get; private set; } = new();
	public List<FrameData> Frames { get; private set; } = new();
	public AnimationGraph? Animation { get; private set; }
	public uint Version { get; private set; }
	public uint Flags { get; private set; }

	public ModelFile( Stream stream ) => ReadFromStream( stream );
	public ModelFile( string path ) => ReadFromFile( path );

	protected override void ReadFromStream( Stream stream )
	{
		if ( stream.CanSeek )
		{
			stream.Seek( 0, SeekOrigin.Begin );
		}

		using var copy = new MemoryStream();
		stream.CopyTo( copy );
		data = copy.ToArray();
		Require( 0, HeaderSize, "MD2 header" );
		if ( U32( 0 ) != Magic )
		{
			throw new InvalidDataException( "Not a Theme Park World MD2 file." );
		}

		Version = U32( 4 );
		if ( Version != 0xDD )
		{
			throw new NotSupportedException( $"MD2 version 0x{Version:X} uses a different or unverified layout." );
		}

		Flags = U32( 0x30 );
		MeshDataList = new List<MeshData>();
		Nodes = new List<NodeData>();
		Frames = new List<FrameData>();
		Animation = null;
		// Animation overlays often copy geometry counts from the base model,
		// but have no geometry sections. Only bit 0 enables those fixups in
		// the original loader. The graph at +0x98 is independent of bit 0.
		if ( (Flags & 1) != 0 )
		{
			ReadGeometry();
		}

		uint graphOffset = U32( 0x98 );
		if ( graphOffset != 0 )
		{
			Animation = ReadAnimation( graphOffset );
		}
	}

	private Matrix4x4 Matrix( uint offset ) => new( F32( offset ),
		F32( offset + 4 ),
		F32( offset + 8 ),
		F32( offset + 0x0C ),
		F32( offset + 0x10 ),
		F32( offset + 0x14 ),
		F32( offset + 0x18 ),
		F32( offset + 0x1C ),
		F32( offset + 0x20 ),
		F32( offset + 0x24 ),
		F32( offset + 0x28 ),
		F32( offset + 0x2C ),
		F32( offset + 0x30 ),
		F32( offset + 0x34 ),
		F32( offset + 0x38 ),
		F32( offset + 0x3C ) );

	private void Require( ulong offset, ulong size, string label )
	{
		if ( offset > (ulong)data.Length || size > (ulong)data.Length - offset )
		{
			throw new InvalidDataException( $"{label}: range 0x{offset:X}+0x{size:X} exceeds MD2 size 0x{data.Length:X}." );
		}
	}

	private void RequireSection( uint offset, ulong size, string label )
	{
		if ( size == 0 )
		{
			return;
		}

		if ( offset == 0 )
		{
			throw new InvalidDataException( $"{label}: missing section offset." );
		}

		Require( offset, size, label );
	}

	private ushort U16( uint offset )
	{
		Require( offset, 2, "u16" );
		return BinaryPrimitives.ReadUInt16LittleEndian( data.AsSpan( (int)offset, 2 ) );
	}

	private uint U32( uint offset )
	{
		Require( offset, 4, "u32" );
		return BinaryPrimitives.ReadUInt32LittleEndian( data.AsSpan( (int)offset, 4 ) );
	}

	private float F32( uint offset )
	{
		Require( offset, 4, "f32" );
		return BitConverter.ToSingle( data, (int)offset );
	}

	private string FixedString( uint offset, int length )
	{
		Require( offset, (uint)length, "fixed string" );
		int end = (int)offset, limit = end + length;
		while ( end < limit && data[end] != 0 )
		{
			end++;
		}

		return Encoding.ASCII.GetString( data, (int)offset, end - (int)offset );
	}

	private string CString( uint offset )
	{
		Require( offset, 1, "string" );
		int end = (int)offset;
		while ( end < data.Length && data[end] != 0 )
		{
			end++;
		}

		if ( end == data.Length )
		{
			throw new InvalidDataException( "Unterminated MD2 string." );
		}

		return Encoding.ASCII.GetString( data, (int)offset, end - (int)offset );
	}
}
