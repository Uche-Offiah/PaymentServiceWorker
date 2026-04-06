using Microsoft.EntityFrameworkCore;
using PaymentService.Worker.Data;
using PaymentService.Worker.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Worker.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly AppDbContext _context;
        public PaymentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistAsync(Guid orderId)
        {
            return await _context.Payments.AnyAsync(p => p.OrderId == orderId);
        }

        public async Task SaveAsync(Payment payment)
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
        }
    }
}
