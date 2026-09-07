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
    public class PodService : IPod
    {
        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly ILogs _logs;
        private readonly IDatabaseObject _db;

        public void Dispose()
        {
            // Optional: implement if needed
        }

        public PodService(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
        }

        public async Task<(int Id, string Name)> SavePodAsync(string podName)
        {
            // Duplicate check (optional but recommended)
            var existing = await _context.Pods
                .FirstOrDefaultAsync(x => x.Name == podName && x.CreatedBy == _session.ReferenceId);

            if (existing != null)
            {
                // Ensure Name is not null to match the non-nullable tuple signature
                return (existing.Id, existing.Name ?? string.Empty);
            }

            var pod = new Pod
            {
                Name = podName,
                IsActive = true,
                CreatedBy = _session.ReferenceId,
                CreatedOn = DateTime.Now
            };

            _context.Pods.Add(pod);
            await _context.SaveChangesAsync();

            return (pod.Id, pod.Name ?? string.Empty);
        }
    }
}
