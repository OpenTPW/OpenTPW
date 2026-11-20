[System.AttributeUsage( AttributeTargets.Field, Inherited = false, AllowMultiple = false )]
sealed class DefaultKeyAttribute : Attribute
{
	public OpenTPW.Key[] Keys;

	public DefaultKeyAttribute( params OpenTPW.Key[] keys )
	{
		Keys = keys;
	}
}
