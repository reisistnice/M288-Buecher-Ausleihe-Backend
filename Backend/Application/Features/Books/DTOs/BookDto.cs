namespace Application.Features.Books.DTOs;

public record BookDto(int Id, string Title, string Author, string ISBN, int TotalCopies, int AvailableCopies);
