namespace MediaFeeder.DTOs.DTOs;

public class FolderTree
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int Unwatched { get; set; }

    public required IList<FolderTree> ChildFolders { get; set; }
}
