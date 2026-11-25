using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Vairas.McpServer.Business;

namespace Vairas.McpServer.Integrations;


public interface IWorkloadTokens { Task<string> GetAsync(CancellationToken ct); }
