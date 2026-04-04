using Microsoft.EntityFrameworkCore;
using PaymentService.Worker.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Worker.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Payment> Payments { get; set; }
        public AppDbContext(DbContextOptions<AppDbContext> options) 
            : base(options) { }
    }
}
