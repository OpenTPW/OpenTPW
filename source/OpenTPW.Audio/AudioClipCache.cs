using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace OpenTPW.Audio;

/// <summary>Per-host PCM cache; concurrent requests for equal resources share a single background decode.</summary>
public sealed class AudioClipCache
{
	private readonly PcmFormat format;
	private readonly ConcurrentDictionary<string, Lazy<Task<float[]>>> decoded = new( StringComparer.Ordinal );
	private readonly ConditionalWeakTable<MP2File, Lazy<Task<float[]>>> files = new();
	public AudioClipCache( PcmFormat format ) { format.Validate(); this.format = format; }
	public float[] Get( MP2File file ) => GetAsync( file ).GetAwaiter().GetResult();
	public Task<float[]> GetAsync( MP2File file )
	{
		ArgumentNullException.ThrowIfNull( file );
		return files.GetValue( file, input =>
		{
			string hash = Convert.ToHexString( SHA256.HashData( input.Data ) );
			return decoded.GetOrAdd( hash, _ => new( () => Task.Run( () => AudioDecoder.Decode( input, format ) ),
				LazyThreadSafetyMode.ExecutionAndPublication ) );
		} ).Value;
	}
}
