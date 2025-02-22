namespace OpenTPW;

/// <summary>
/// Animations, objects, etc.
/// </summary>
public class ScriptDefs
{
	public enum Animations
	{
		// Animations
		ANIM_Create = 0,
		ANIM_Idle = 2,
		ANIM_Load = 3,
		ANIM_Start = 4,
		ANIM_Main = 5,
		ANIM_End = 6,
		ANIM_Unload = 7,
		ANIM_Break = 9,
		ANIM_Repair = 10,
		ANIM_Other = 11,
	}

	public enum Effects
	{
		// Particle / sound effects
		OBJ_PTCL = 1,
		OBJ_SOUND_LOC_AMB = 1,
		OBJ_PTCL_D = 2,
		OBJ_SOUND_LOC_RID = 3,
		OBJ_SOUND_GLO_RID = 5,
		OBJ_SOUND_GLO_KID = 6,
		OBJ_SOUND_GLO_STA = 7,
		OBJ_SOUND_GLO_AMB = 8,
		OBJ_SOUND_GLO_UI = 9,
		OBJ_SOUND_GLO_BMP = 11,
	}

	public enum Coaster
	{
		// Rollercoaster specific
		COAST_ADDPEEP = 1,
		COAST_GETQUEUE = 2,
		COAST_GETPEEP = 3,
		COAST_SETBROKE = 4,
		COAST_SETCLOSED = 5,
		COAST_SETCAPACITY = 6,
		COAST_SETWORN = 7,
		COAST_INITIALISE = 8,

		SPACE_ENGINE_REVS = 16,
		BUS_CTRL = 20,
	}

	public enum Walk
	{
		// WALKON actions
		WALK_ACTION_VANISH = 1,
		WALK_ACTION_BEAM = 2,
		WALK_ACTION_HEAD = 4,
		WALK_ACTION_THROW = 5,
		WALK_ACTION_CHEER = 6,
	}

	public enum Bumper
	{
		// Bumper (DinoKart, Space Water Ride) specific
		BUMP_LAUNCHCAR = -1,
		BUMP_HALTRIDE = 0,
		BUMP_PEEPOFF = 7,
		BUMP_STARTRACE = 32,
		BUMP_OPENRIDE = 38,
		BUMP_PEEPON = 47,
		BUMP_ISTRACKVALID = 54,
		BUMP_SETBROKEN = 93,
		BUMP_CLOSERIDE = 115,
		BUMP_CARSONRIDE = 121,
		BUMP_SETLAPS = 134,
		BUMP_WATERCLOSED = 18770
	}
}
