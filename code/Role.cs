public class Role : IValid
{
	public string Id { get; set; }
	public string Name { get; set; }

	public bool IsValid => this != null;

	public Role(string id, string name = "")
	{
		Id = id;
		Name = name;
	}
}
