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
    public class CompanyService : ICompany
    {
        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly ILogs _logs;
        private readonly IDatabaseObject _db;

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public CompanyService(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
        }

        public async Task<(int Id, string Name, string NameAr)> SaveCompanyAsync(string companyName, string companyNameAr)
        {
            // Duplicate check (optional but recommended)
            var existing = await _context.Companies
                .FirstOrDefaultAsync(x => x.Name == companyName && x.CreatedBy == _session.LoginId);

            if (existing != null)
            {
                // Ensure non-null values for Name and NameAr to match the non-nullable tuple signature
                return (existing.Id, existing.Name ?? string.Empty, existing.NameAr ?? string.Empty);
            }

            var company = new Company
            {
                Name = companyName,
                NameAr = companyNameAr,
                IsActive = true,
                CreatedBy = _session.LoginId,
                CreatedOn = DateTime.Now
            };

            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            return (company.Id, company.Name ?? string.Empty, company.NameAr ?? string.Empty);
        }
    }
}
