namespace MediaFeeder.Data.DTOs;

[Serializable]
public sealed record FolderResponse(string Name, int Id, IList<int> ChildFolders, IList<int> ChildSubscriptions)
{
    public override string ToString() => $"{{ Name = {Name}, Id = {Id}, ChildFolders = {ChildFolders}, ChildSubscriptions = {ChildSubscriptions} }}";
}
