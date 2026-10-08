using System.Collections.Generic;

namespace BusinessLogic.Models
{
    /// <summary>
    /// Maps raw foreign-key ids to human readable names so audit descriptions and
    /// the change-detail popup never show bare ids like "customer #1".
    /// </summary>
    public class AuditNameMap
    {
        /// <summary>Lookup for the popup: "CustomerId:1" -> "ABC Trading".</summary>
        public Dictionary<string, string> Lookup { get; set; } = new Dictionary<string, string>();

        public void AddCustomer(int id, string? name)
        {
            if (id > 0 && !string.IsNullOrWhiteSpace(name))
            {
                Lookup[$"CustomerId:{id}"] = name!;
                Lookup[$"ServiceProviderId:{id}"] = name!;
            }
        }

        public void AddJob(int id, string? jobNumber)
        {
            if (id > 0 && !string.IsNullOrWhiteSpace(jobNumber))
            {
                Lookup[$"JobId:{id}"] = jobNumber!;
            }
        }

        public string? Resolve(string? fieldName, string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(rawValue)) return null;
            return Lookup.TryGetValue($"{fieldName}:{rawValue}", out var name) ? name : null;
        }
    }
}
