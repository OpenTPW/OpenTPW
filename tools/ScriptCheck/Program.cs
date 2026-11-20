using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OpenTPW;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if ( args.Length != 3 )
	throw new ArgumentException( "Usage: ScriptCheck <data-directory> <output-directory> <expected-count>" );
string data = Path.GetFullPath( args[0] ), output = Path.GetFullPath( args[1] );
Directory.CreateDirectory( output );
int expected = int.Parse( args[2] ), count = 0;
var sources = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
var manifest = new List<string> { "index\tsource\tsha256\tinstructions\tticks\tcalls" };
foreach ( var (name, bytes) in Inputs( data ) )
{
	if ( !sources.Add( name ) ) throw new InvalidDataException( $"Duplicate script source: {name}" );
	string stem = Path.Combine( output, $"{count++:D3}" );
	File.WriteAllBytes( stem + ".rse", bytes );
	using var stream = new MemoryStream( bytes );
	var script = new RideScriptFile( stream );
	int instructions = ScriptDisassembler.Disassemble( script ).Count();
	using var trace = new StreamWriter( stem + ".cs.trace", false, new UTF8Encoding( false ) );
	var host = new TraceHost( trace );
	var vm = new RideVM( script, host );
	// A broken script can loop forever inside a free critical section; a separate timer
	// makes this an explicit failing process instead of silently weakening VM semantics.
	using var watchdog = new Timer( _ => Environment.FailFast( $"Script exceeded 10 seconds of wall time: {name}" ), null, 10_000, Timeout.Infinite );
	uint tick = 0;
	for ( ; host.Clock <= 60_000 && !vm.IsStopped; host.Clock += 33 ) vm.Tick( tick++ );
	trace.WriteLine( $"; {(vm.IsStopped ? "stopped" : "still running")} after {tick} ticks, {host.Calls} game calls, LV {vm.LV}" );
	for ( int i = 0; i < vm.Variables.Length; i++ ) trace.WriteLine( $";   {script.VariableNames[i],-20} = {vm.Variables[i]}" );
	if ( vm.IsStopped ) throw new InvalidOperationException( $"Script stopped before 60 seconds: {name}" );
	manifest.Add( $"{count - 1:D3}\t{name}\t{Convert.ToHexString( SHA256.HashData( bytes ) ).ToLowerInvariant()}\t{instructions}\t{tick}\t{host.Calls}" );
}
File.WriteAllLines( Path.Combine( output, "manifest.tsv" ), manifest );
if ( count != expected ) throw new InvalidDataException( $"Expected {expected} scripts, found {count}." );
Console.WriteLine( $"{count} scripts ran 60000 ms, 0 stopped, 0 failed" );

static IEnumerable<(string Name, byte[] Bytes)> Inputs( string data )
{
	foreach ( var path in Directory.EnumerateFiles( data, "*", SearchOption.AllDirectories ).Order( StringComparer.OrdinalIgnoreCase ) )
	{
		string relative = Path.GetRelativePath( data, path ).Replace( '\\', '/' );
		if ( Path.GetExtension( path ).Equals( ".rse", StringComparison.OrdinalIgnoreCase ) )
			yield return (relative, File.ReadAllBytes( path ));
		else if ( Path.GetExtension( path ).Equals( ".wad", StringComparison.OrdinalIgnoreCase ) )
		{
			using var wad = new WadArchive( path );
			foreach ( var member in Walk( wad.Root, "" ) ) yield return member;
		}

		IEnumerable<(string, byte[])> Walk( ArchiveDirectory directory, string prefix )
		{
			foreach ( var child in directory.Children.OrderBy( x => x.Name, StringComparer.OrdinalIgnoreCase ) )
			{
				string name = prefix + child.Name;
				if ( child is ArchiveDirectory nested )
					foreach ( var member in Walk( nested, name + "/" ) ) yield return member;
				else if ( child is WadArchiveFile file && Path.GetExtension( name ).Equals( ".rse", StringComparison.OrdinalIgnoreCase ) )
				{
					try { yield return (relative + ":" + name, file.GetData()); }
					finally { file.Free(); }
				}
			}
		}
	}
}

/// <summary>Deterministic services matching the independent C++ rse-run diagnostic host.</summary>
sealed class TraceHost( TextWriter output ) : IRideVMHost
{
	public int Clock;
	public int Calls;
	private uint seed = 12345;
	public int Now => Clock;
	// These are the diagnostic tool's LCG parameters, not the retail game's random generator.
	public uint Random() => seed = unchecked(seed * 1103515245u + 12345u);
	public int? Call( RideVM vm, Opcode opcode, uint[] operands )
	{
		output.WriteLine( $"{Clock,7} ms  pc {vm.ProgramCounter:x4}  {opcode,-15} {string.Join( ", ", operands.Select( x => ScriptDisassembler.FormatOperand( vm.Script, x ) ) )}" );
		Calls++;
		string name = opcode.ToString();
		if ( name.Contains( "ANIM", StringComparison.Ordinal ) && !name.Contains( "FLUSH", StringComparison.Ordinal ) ) vm.AnimationDeadline = Clock + 1000;
		return null;
	}
	public int PlayAnimation( RideVM vm, int slot, int variant, bool loop, float speed )
	{
		char letter = slot is >= 0 and < 12 ? "CDILSMEUWBRO"[slot] : '?';
		output.WriteLine( $"{Clock,7} ms  pc {vm.ProgramCounter:x4}  {(loop ? "loop anim" : "play anim"),-15} slot {slot} ({letter}), variant {variant}" );
		Calls++;
		return 1000;
	}
	public void FlushAnimation( RideVM vm )
	{
		output.WriteLine( $"{Clock,7} ms  pc {vm.ProgramCounter:x4}  flush anim" );
		Calls++;
	}
}
