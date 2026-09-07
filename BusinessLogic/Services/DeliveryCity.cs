using Azure.Core;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using BusinessLogic.Helper;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Reflection.Metadata.BlobBuilder;

namespace BusinessLogic.Services
{
    public class DeliveryCityService : IDeliveryCity
    {
        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly ILogs _logs;
        private readonly IDatabaseObject _db;

        public void Dispose()
        {
            // Optional
        }

        public DeliveryCityService(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
        }

        public async Task<(int Id, string Name)> SaveDeliveryCityAsync(string deliveryCityName)
        {
            // Duplicate check
            var existing = await _context.DeliveryCities
                .FirstOrDefaultAsync(x => x.Name == deliveryCityName && x.CreatedBy == _session.ReferenceId);

            if (existing != null)
            {
                // Ensure Name is not null to satisfy the non-nullable tuple contract
                return (existing.Id, existing.Name ?? string.Empty);
            }

            var city = new DeliveryCity
            {
                Name = deliveryCityName,
                IsActive = true,
                CreatedBy = _session.ReferenceId,
                CreatedOn = DateTime.Now
            };

            _context.DeliveryCities.Add(city);
            await _context.SaveChangesAsync();

            return (city.Id, city.Name ?? string.Empty);
        }
    }
}
