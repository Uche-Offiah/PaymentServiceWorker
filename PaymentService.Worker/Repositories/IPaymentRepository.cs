using PaymentService.Worker.Entities;
using PaymentService.Worker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Worker.Repositories
{
    public interface IPaymentRepository
    {
        Task<bool> ExistAsync(Guid OrderId);
        Task SaveAsync (Payment payment);
    }
}
