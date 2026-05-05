namespace Core.Entities;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
