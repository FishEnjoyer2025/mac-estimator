namespace MacEstimator.App.Models;

public class Room
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Room 1";
    public List<LineItem> LineItems { get; set; } = [];
}
