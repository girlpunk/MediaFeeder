namespace MediaFeeder.DTOs.DTOs;

public class FolderGet
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public required IList<int> ChildFolders { get; set; }
    public required IList<int> ChildSubscriptions { get; set; }

    // public int Unwatched { get; set; }
}
