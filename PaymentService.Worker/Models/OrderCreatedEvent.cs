using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Worker.Models
{
    public class OrderCreatedEvent
    {
        public Guid OrderId { get; set; }
        public Decimal Amount { get; set; }
    }
}
