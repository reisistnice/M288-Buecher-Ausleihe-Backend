using System.Security.Claims;
using Application.Common.Interfaces;
using Application.Features.Books.DTOs;
using Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/books")]
[Authorize]
public class BooksController : ControllerBase
{
    private readonly IBookRepository _bookRepository;

    public BooksController(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var books = await _bookRepository.GetAllAsync();
        var dtos = books.Select(b => new BookDto(b.Id, b.Title, b.Author, b.ISBN, b.IsAvailable));
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var book = await _bookRepository.GetByIdAsync(id);
        if (book is null) return NotFound();
        return Ok(new BookDto(book.Id, book.Title, book.Author, book.ISBN, book.IsAvailable));
    }

    [HttpPost]
    [Authorize(Roles = "Administrators")]
    public async Task<IActionResult> Create([FromBody] CreateBookDto dto)
    {
        var book = new Book { Title = dto.Title, Author = dto.Author, ISBN = dto.ISBN };
        var created = await _bookRepository.CreateAsync(book);
        return CreatedAtAction(nameof(GetById), new { id = created.Id },
            new BookDto(created.Id, created.Title, created.Author, created.ISBN, created.IsAvailable));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrators")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _bookRepository.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
