namespace Application.Features.Loans.DTOs;

public record LoanDto(int Id, int BookId, string BookTitle, int UserId, string Username, DateTime LoanDate, DateTime? ReturnDate);
