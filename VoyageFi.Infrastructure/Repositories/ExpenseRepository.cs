using Microsoft.EntityFrameworkCore;
using VoyageFi.Core.Entities;
using VoyageFi.Core.Interfaces;
using VoyageFi.Infrastructure.Data;

namespace VoyageFi.Infrastructure.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly VoyageFiDbContext _context;

    public ExpenseRepository(VoyageFiDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Expense>> GetAllExpensesAsync()
    {
        return await _context.Expenses.ToListAsync();
    }

    public async Task<Expense?> GetExpenseByIdAsync(Guid id)
    {
        return await _context.Expenses.FindAsync(id);
    }

    public async Task<Expense> AddExpenseAsync(Expense expense)
    {
        await _context.Expenses.AddAsync(expense);
        await _context.SaveChangesAsync();
        return expense;
    }

    public async Task UpdateExpenseAsync(Expense expense)
    {
        _context.Expenses.Update(expense);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteExpenseAsync(Guid id)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense != null)
        {
            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();
        }
    }
}