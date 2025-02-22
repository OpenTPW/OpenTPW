namespace OpenTPW;

partial class Refpack
{
	internal interface IRefpackCommand
	{
		bool StopAfterFound => false;
		int Length { get; }

		void Decompress( byte[] data, ref List<byte> decompressedData, int offset, out uint skipAhead );
		bool OpcodeMatches( byte firstByte );
	}

	public static void DecompressData( byte[] data, ref List<byte> outputData, int offset, int opcodeLength, uint proceedingDataLength, uint referencedDataLength, uint referencedDataOffset )
	{
		for ( var i = 0; i < proceedingDataLength; ++i ) // Proceeding data comes from the source buffer (compressed data)
		{
			var pos = (uint)(offset + opcodeLength + i);
			if ( pos >= data.Length )
				break; // Prevent any overflowing

			outputData.Add( data[pos] );
		}

		var outputDataLen = outputData.Count;

		for ( var i = 0; i < referencedDataLength; ++i ) // Referenced data comes from the output buffer (decompressed data)
		{
			var pos = (int)(outputDataLen - referencedDataOffset);
			if ( pos < 0 || pos >= outputData.Count )
				break; // Prevent any overflowing

			outputData.Add( outputData[pos + i] );
		}
	}

	internal class FourByteCommand : IRefpackCommand
	{
		public int Length => 4;

		public void Decompress( byte[] data, ref List<byte> decompressedData, int offset, out uint skipAhead )
		{
			var proceedingDataLength = (uint)((data[offset] & 0x03));
			var referencedDataLength = (uint)(((data[offset] & 0x0C) << 6) + data[offset + 3] + 5);
			var referencedDataDistance = (uint)(((data[offset] & 0x10) << 12) + (data[offset + 1] << 8) + data[offset + 2] + 1);
			skipAhead = proceedingDataLength;

			DecompressData( data, ref decompressedData, offset, Length, proceedingDataLength, referencedDataLength, referencedDataDistance );
		}

		public bool OpcodeMatches( byte firstByte ) => firstByte.GetBits( 0, 1, 2 ).ValuesEqual( new[] {
			true,
			true,
			false
		} );
	}

	internal class ThreeByteCommand : IRefpackCommand
	{
		public int Length => 3;

		public void Decompress( byte[] data, ref List<byte> decompressedData, int offset, out uint skipAhead )
		{
			var proceedingDataLength = (uint)((data[offset + 1] & 0xC0) >> 6);
			var referencedDataLength = (uint)((data[offset] & 0x3F) + 4);
			var referencedDataDistance = (uint)(((data[offset + 1] & 0x3F) << 8) + data[offset + 2] + 1);
			skipAhead = proceedingDataLength;

			DecompressData( data, ref decompressedData, offset, Length, proceedingDataLength, referencedDataLength, referencedDataDistance );
		}

		public bool OpcodeMatches( byte firstByte ) => firstByte.GetBits( 0, 1 ).ValuesEqual( new[] {
			true,
			false
		} );
	}

	internal class TwoByteCommand : IRefpackCommand
	{
		public int Length => 2;

		public void Decompress( byte[] data, ref List<byte> decompressedData, int offset, out uint skipAhead )
		{
			var proceedingDataLength = (uint)((data[offset] & 0x03));
			var referencedDataLength = (uint)(((data[offset] & 0x1C) >> 2) + 3);
			var referencedDataDistance = (uint)(((data[offset] & 0x60) << 3) + data[offset + 1] + 1);
			skipAhead = proceedingDataLength;

			DecompressData( data, ref decompressedData, offset, Length, proceedingDataLength, referencedDataLength, referencedDataDistance );
		}
		public bool OpcodeMatches( byte firstByte ) => !firstByte.GetBit( 0 );
	}

	internal class OneByteCommand : IRefpackCommand
	{
		public int Length => 1;

		public void Decompress( byte[] data, ref List<byte> decompressedData, int offset, out uint skipAhead )
		{
			var dataAtOffset = data[offset];
			var proceedingDataLength = (uint)(((dataAtOffset & 0x1F) + 1) << 2);

			skipAhead = proceedingDataLength;
			DecompressData( data, ref decompressedData, offset, Length, proceedingDataLength, 0, 0 );
		}

		public bool OpcodeMatches( byte firstByte ) => ((firstByte & 0x1F) + 1) << 2 <= 0x70 && firstByte.GetBits( 0, 1, 2 ).ValuesEqual( new[] {
			true,
			true,
			true
		} );
	}

	internal class StopCommand : IRefpackCommand
	{
		public int Length => 1;
		public bool StopAfterFound => true;

		public void Decompress( byte[] data, ref List<byte> decompressedData, int offset, out uint skipAhead )
		{
			var proceedingDataLength = (uint)((data[offset] & 0x03));
			skipAhead = proceedingDataLength;
			DecompressData( data, ref decompressedData, offset, Length, proceedingDataLength, 0, 0 );
		}

		public bool OpcodeMatches( byte firstByte ) => ((firstByte & 0x1F) + 1) << 2 > 0x70 && firstByte.GetBits( 0, 1, 2 ).ValuesEqual( new[] {
			true,
			true,
			true
		} );
	}
}
