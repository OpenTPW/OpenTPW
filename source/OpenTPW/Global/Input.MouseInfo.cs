using Newtonsoft.Json.Linq;

namespace OpenTPW;

public static partial class Input
{
	public struct MouseInfo
	{
		public Vector2 Delta;
		public Vector2 Position;

		public bool Left;
		public bool Right;

		public float Wheel;

		public override string ToString()
		{
			var str = "";

			foreach ( var field in typeof( MouseInfo ).GetFields() )
				str += $"{field.Name}: {field.GetValue( this )}\n";

			return str;
		}
	}
}
