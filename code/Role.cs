using System;

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

	public Role( int id, string name = "" )
	{
		Id = id.ToString();
		Name = name;
	}

	public bool Check(Enum enumValue)
	{
		if ( !this.IsValid() ) return false;

		return (Id.ToLower() == enumValue.ToString().ToLower());
	}
}
