# ScriptCheck

Runs every loose RSE and WAD-contained RSE in a data directory for 60 seconds of simulated time. Requires .NET 10; comparison also uses Python's standard library and an existing independent C++ `tpi` diagnostic executable. Game directories are read only. Output contains extracted script bytes, complete traces and a source/hash manifest; keep it outside the repository.

From the repository root:

```powershell
dotnet build tools/ScriptCheck/ScriptCheck.csproj
dotnet tools/ScriptCheck/bin/Debug/net10.0/ScriptCheck.dll <data-directory> <output-directory> <expected-count>
python -I tools/ScriptCheck/compare.py <output-directory> <tpi-executable> <expected-count>
```

Use expected count 308 for Theme Park World or 292 for Theme Park Inc. Each script gets a fresh diagnostic host: clock 0, 33 ms increments, ID 0, speed 50, zero variables, LCG seed 12345, one-second animations and no query writeback. These services are comparison fixtures, not game-system implementations.

The comparison excludes only the C++ input-path header, and otherwise compares every line, including raw formatted operands, animation slots/variants, timestamps, PCs, termination status, LV and final variables. Both processes fail on missing data, early termination, malformed output or timeout. A difference writes a `.diff` beside the two traces and returns nonzero. `comparison.json` includes the reference executable hash.
