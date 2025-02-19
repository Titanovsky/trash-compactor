public class RoleHandler
{
	public static List<string> Roles { get; private set; } = new();

	public void Add(string role)
	{
		Roles.Add(role);
	}

	public void Remove(string role)
	{
		Roles.Remove(role);
	}

	public string Get( string role )
	{
		if (!Roles.Contains( role )) return null;

		return role;
	}
}
