public class Role
{
	public string Id { get; set; }
	public string Name { get; set; }

	public Role(string id, string name = "")
	{
		Id = id;
		Name = name;
	}
}
