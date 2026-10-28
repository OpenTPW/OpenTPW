using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>The sixteen ESP sequence descriptors read by TP.exe 0x53d3b0.</summary>
public sealed class SpriteSequenceFile
{
	public sealed record Sequence( int FirstFrame, int FrameCount, bool Directional );
	public string TextureName { get; }
	public byte[] HeaderFlags { get; }
	public byte[] TailMetadata { get; }
	public IReadOnlyList<Sequence> Sequences { get; }

	public SpriteSequenceFile( byte[] data )
	{
		if ( data.Length is not (334 or 350) || Encoding.ASCII.GetString( data, 0, 12 ) != "ESP_FILE2.00" )
			throw new InvalidDataException( "Unsupported or truncated ESP descriptors" );
		int nameEnd = Array.IndexOf( data, (byte)0, 12, 256 );
		if ( nameEnd < 0 ) throw new InvalidDataException( "Unterminated ESP texture name" );
		TextureName = Encoding.ASCII.GetString( data, 12, nameEnd - 12 );
		HeaderFlags = data[268..270];
		TailMetadata = data[334..];
		var sequences = new Sequence[16];
		for ( int i = 0; i < sequences.Length; i++ )
		{
			int offset = 270 + i * 4;
			sequences[i] = new Sequence( BinaryPrimitives.ReadUInt16LittleEndian( data.AsSpan( offset, 2 ) ), data[offset + 2], data[offset + 3] != 0 );
		}
		Sequences = sequences;
	}

	public int FrameIndex( int sequence, int frame, int direction, int textureFrameCount )
	{
		if ( (uint)sequence >= Sequences.Count ) throw new ArgumentOutOfRangeException( nameof(sequence) );
		var entry = Sequences[sequence];
		if ( (uint)frame >= entry.FrameCount ) throw new ArgumentOutOfRangeException( nameof(frame) );
		// 0x53e9d0 masks direction only for a directional descriptor.
		int index = entry.FirstFrame + frame + entry.FrameCount * (entry.Directional ? direction & 7 : 0);
		if ( index >= textureFrameCount ) throw new InvalidDataException( "ESP sequence exceeds its texture frames" );
		return index;
	}
}
