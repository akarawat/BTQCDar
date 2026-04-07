using System.Net.Http.Json;
using System.Text.Json;

namespace BTQCDar.Services
{
    // ─── Response models ─────────────────────────────────────────────────────

    public class DsApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }

    public class DsSignResult
    {
        public bool IsSuccess { get; set; }
        public string SignatureBase64 { get; set; } = string.Empty;
        public string SignedBy { get; set; } = string.Empty;
        public DateTime SignedAt { get; set; }
        public string CertThumbprint { get; set; } = string.Empty;
        public DateTime CertExpiry { get; set; }
        public string ReferenceId { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    // ─── Audit models (from GET /api/audit/reference/{referenceId}) ───────────

    public class DsAuditRecord
    {
        public long Id { get; set; }
        public string ReferenceId { get; set; } = string.Empty;
        public string SignedByUser { get; set; } = string.Empty;
        public string? SignerFullName { get; set; }
        public string? SignerRole { get; set; }
        public string SignedByCert { get; set; } = string.Empty;
        public string CertThumbprint { get; set; } = string.Empty;
        public DateTime CertExpiry { get; set; }
        public DateTime SignedAt { get; set; }
        public string DataHash { get; set; } = string.Empty;
        public string SignatureType { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public string? Department { get; set; }
        public string? Remarks { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevocationReason { get; set; }
    }

    public class DsAuditData
    {
        public string ReferenceId { get; set; } = string.Empty;
        public List<DsAuditRecord> Records { get; set; } = new();
        public int Total { get; set; }
    }

    // ─── Interface ───────────────────────────────────────────────────────────

    public interface IDigitalSignService
    {
        Task<DsSignResult?> SignDarAsync(
            string darNo, string signerSamAcc, string role,
            string purpose, string department, string? remarks = null);

        Task<DsAuditData?> GetAuditAsync(string darNo);
    }

    // ─── Implementation ──────────────────────────────────────────────────────

    public class DigitalSignService : IDigitalSignService
    {
        private readonly HttpClient _http;
        private readonly ILogger<DigitalSignService> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public DigitalSignService(HttpClient http, ILogger<DigitalSignService> logger)
        {
            _http = http;
            _logger = logger;
        }

        // ── Sign ──────────────────────────────────────────────────────────────
        public async Task<DsSignResult?> SignDarAsync(
            string darNo, string signerSamAcc, string role,
            string purpose, string department, string? remarks = null)
        {
            try
            {
                var payload = new
                {
                    DataToSign = darNo,
                    ReferenceId = darNo,
                    Purpose = purpose,
                    Department = department,
                    Remarks = remarks ?? string.Empty,
                    signerUsername = signerSamAcc
                };

                var response = await _http.PostAsJsonAsync("/api/sign", payload);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[DigitalSign] Sign API {status} for {darNo}: {body}",
                        (int)response.StatusCode, darNo, body);
                    return null;
                }

                var wrapped = JsonSerializer.Deserialize<DsApiResponse<DsSignResult>>(body, _json);
                if (wrapped?.Success == true && wrapped.Data?.IsSuccess == true)
                    return wrapped.Data;

                _logger.LogWarning("[DigitalSign] Sign failed for {darNo}: {msg}",
                    darNo, wrapped?.Data?.ErrorMessage ?? wrapped?.Message ?? "unknown");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DigitalSign] Exception signing {darNo}", darNo);
                return null;
            }
        }

        // ── Audit Log ─────────────────────────────────────────────────────────
        public async Task<DsAuditData?> GetAuditAsync(string darNo)
        {
            try
            {
                var response = await _http.GetAsync($"/api/audit/reference/{darNo}");
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[DigitalSign] Audit API {status} for {darNo}",
                        (int)response.StatusCode, darNo);
                    return null;
                }

                var wrapped = JsonSerializer.Deserialize<DsApiResponse<DsAuditData>>(body, _json);
                return wrapped?.Success == true ? wrapped.Data : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DigitalSign] Exception getting audit for {darNo}", darNo);
                return null;
            }
        }
    }
}
