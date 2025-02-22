namespace OpenTPW;

public class Operand
{
	public enum Type
	{
		Variable,
		Literal,
		String,
		Location
	}

	private int value;
	private readonly RideVM vmInstance;
	public readonly Type type;
	private readonly int nameIndex;

	public Operand( RideVM vmInstance, Type type, int value, int nameIndex = -1 )
	{
		this.vmInstance = vmInstance;
		this.type = type;
		this.value = value;
		this.nameIndex = nameIndex;
	}

	public int Value
	{
		get
		{
			switch ( type )
			{
				case Type.Variable:
					return vmInstance.Variables[value];
				default:
					return value;
			}
		}
		set
		{
			switch ( type )
			{
				case Type.Variable:
					vmInstance.Variables[this.value] = value;
					break;
				default:
					this.value = value;
					break;
			}
		}
	}

	public override string ToString()
	{
		switch ( type )
		{
			case Type.Variable:
				return vmInstance.VariableNames[nameIndex];
			case Type.Location:
				return $"label_{Value}";
			case Type.String:
				return $"\"{vmInstance.Strings[value]}\"";
			default:
				return Value.ToString();
		}
	}
}
