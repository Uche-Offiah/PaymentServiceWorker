using PaymentService.Worker.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Worker.Repositories
{
    public interface IPaymentRepository
    {
        Task<bool> ExistAsync();
        Task SaveAsync (Payment payment);
    }
}
