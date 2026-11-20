# OpenTPW.UI

The retail UI core shared by Theme Park World and Theme Park Inc: both games' executables carry the same UI library, so it is ported once. It has no rendering and no dependencies; drawing is done by the game on top of `OpenTPW.Graphics`.

- **Tables**: `UiTable.Read` interprets a retail UI table (16-bit words: an opcode and its operands) into an element tree, with the Theme Park Inc dialect adding opcode 0x13. Rectangles are left, top, right, bottom in 2048x1536 units.
- **Elements**: `UiElement` keeps type, flags, identifier, rectangle, hit shape (rectangle, circle, ellipse or polygon), model-name hash and the values pages store; `HitTest`, `Find` and `SetEnabled` follow the retail rules.
- **Model names**: `UiModelName.Hash` is the retail root-name hash (multiplier 47) that tables use to refer to control models.
- **Input**: `UiInput` routes pointer moves and buttons (enter, leave, move, down, up), and runs the button behaviour: the six control states, clicks, toggles and auto-repeat. `UiInputOptions` holds the one difference between the two games' button handlers.
- **Startup images & movies**: `StartupImages` and `StartupMovies` port `StartupImages_Prepare` (US `0x587380` / TPI `0x6f89b0`), `StartupMovie_Play` (US `0x54df20` / TPI `0x6a1780`) and `StartupMovie_PlayRandom` (US `0x54e070` / TPI `0x6a1930`), parameterised by `StartupImageOptions` and `StartupMovieOptions` for both games.

```csharp
var table = UiTable.Read( words, UiTableDialect.ThemeParkInc );
var input = new UiInput( UiInputOptions.ThemeParkInc ) { Root = table.Root };
input.Message += m => { if ( m.Kind == UiMessageKind.Click ) HandleClick( m.Target.Id ); };
input.PointerMove( x, y ); // 2048x1536 units
```

Retail addresses are in the code comments (US `TP.exe`, and TPI `game.exe` where it differs).
