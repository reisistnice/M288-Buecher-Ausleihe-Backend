using System.Security.Claims;
using Application.Common.Interfaces;
using Application.Features.Loans.DTOs;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly ILoanRepository _loanRepository;

    public LoansController(ILoanRepository loanRepository)
    {
        _loanRepository = loanRepository;
    }

    [HttpGet]
    [Authorize(Roles = "Administrators")]
    public async Task<IActionResult> GetAll()
    {
        var loans = await _loanRepository.GetAllAsync();
        return Ok(loans.Select(ToDto));
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.SerialNumber)!);
        var loans = await _loanRepository.GetByUserIdAsync(userId);
        return Ok(loans.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoanDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.SerialNumber)!);
        var (loan, error) = await _loanRepository.BorrowAsync(dto.BookId, userId);

        if (loan is null)
        {
            return error switch
            {
                "BOOK_NOT_FOUND"          => NotFound(new { message = "Book not found." }),
                "NO_COPIES_AVAILABLE"     => Conflict(new { message = "NO_COPIES_AVAILABLE" }),
                _                         => StatusCode(503, new { message = "Service temporarily unavailable. Please retry." })
            };
        }

        return CreatedAtAction(nameof(GetAll), ToDto(loan));
    }

    [HttpPut("{id}/return")]
    public async Task<IActionResult> Return(int id)
    {
        var loan = await _loanRepository.ReturnAsync(id);
        if (loan is null) return NotFound("Loan not found or already returned.");
        return Ok(ToDto(loan));
    }

    private static LoanDto ToDto(Loan l) =>
        new(l.Id, l.BookId, l.Book.Title, l.UserId, l.User.Username, l.LoanDate, l.ReturnDate);
}
