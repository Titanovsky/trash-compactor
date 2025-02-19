public static class RoleHandler
{
	public static Dictionary<string, Role> Roles { get; private set; } = new();

	public static void Add(Role role)
	{
		if (Roles.ContainsKey(role.Id))
		{
			Log.Error( $"An attemption to add the role {role.Name} ({role.Id}) with similiar id" );

			return;
		}

		Roles.Add(role.Id, role);
	}

	public static void Remove(Role role)
	{
		if ( !Roles.TryGetValue( role.Id, out Role currentRole ) ) return;
		if ( currentRole != role ) return;

		Remove( role.Id );
	}

	public static void Remove( string id )
	{
		Roles.Remove( id );
	}

	public static Role Get( string id )
	{
		if ( !Roles.TryGetValue(id, out Role result) ) return null;

		return result;
	}

	public static bool Exists( string id )
	{
		return Roles.ContainsKey( id );
	}

	public static bool Exists( Role role )
	{
		return Roles.ContainsKey( role.Id ) && Roles[role.Id] == role;
	}
}
