using Microsoft.AspNetCore.Mvc;
using VoyageFi.Application.DTOs;
using VoyageFi.Core.Entities;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseRepository _expenseRepository;

    public ExpensesController(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    [HttpPost]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto request)
    {
        var expense = new Expense
        {
            Amount = request.Amount,
            Currency = request.Currency,
            Category = request.Category,
            Date = request.Date,
            Description = request.Description,
            TripId = request.TripId
        };

        var createdExpense = await _expenseRepository.AddExpenseAsync(expense);

        var response = new ExpenseResponseDto
        {
            Id = createdExpense.Id,
            Amount = createdExpense.Amount,
            Currency = createdExpense.Currency,
            Category = createdExpense.Category,
            Date = createdExpense.Date,
            Description = createdExpense.Description,
            TripId = createdExpense.TripId
        };

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllExpenses()
    {
        var expenses = await _expenseRepository.GetAllExpensesAsync();
        
        var responseList = expenses.Select(e => new ExpenseResponseDto
        {
            Id = e.Id,
            Amount = e.Amount,
            Currency = e.Currency,
            Category = e.Category,
            Date = e.Date,
            Description = e.Description,
            TripId = e.TripId
        }).ToList();

        return Ok(responseList);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] CreateExpenseDto request)
    {
        var existingExpense = await _expenseRepository.GetExpenseByIdAsync(id);
        if (existingExpense == null) return NotFound("Harcama bulunamadı.");

        existingExpense.Amount = request.Amount;
        existingExpense.Currency = request.Currency;
        existingExpense.Category = request.Category;
        existingExpense.Date = request.Date;
        existingExpense.Description = request.Description;

        await _expenseRepository.UpdateExpenseAsync(existingExpense);
        return Ok("Harcama başarıyla güncellendi.");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        var existingExpense = await _expenseRepository.GetExpenseByIdAsync(id);
        if (existingExpense == null) return NotFound("Harcama bulunamadı.");

        await _expenseRepository.DeleteExpenseAsync(id);
        return Ok("Harcama başarıyla silindi.");
    }
}