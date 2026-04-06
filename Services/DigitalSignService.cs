using System.Net.Http.Json;
using System.Text.Json;

namespace BTQCDar.Services
{
    // ─── Response models from BTDigitalSign API ──────────────────────────────

    public class DsApiResponse<T>
    {
        public bool   Success   { get; set; }
        public T?     Data      { get; set; }
        public string? Message  { get; set; }
    }

    public class DsSignResult
    {
        public bool   IsSuccess         { get; set; }
        public string SignatureBase64   { get; set; } = string.Empty;  // cryptographic signature
        public string SignedBy          { get; set; } = string.Empty;  // SamAcc
        public DateTime SignedAt        { get; set; }
        public string CertThumbprint    { get; set; } = string.Empty;
        public DateTime CertExpiry      { get; set; }
        public string ReferenceId       { get; set; } = string.Empty;
        public string? ErrorMessage     { get; set; }
    }

    // ─── Interface ───────────────────────────────────────────────────────────

    public interface IDigitalSignService
    {
        /// <summary>
        /// Sign a DAR — calls POST /api/sign.
        /// Returns DsSignResult on success, null on network/API failure.
        /// </summary>
        Task<DsSignResult?> SignDarAsync(
            string darNo,
            string signerSamAcc,
            string role,        // "Reviewer" or "Approver"
            string purpose,
            string department,
            string? remarks = null);
    }

    // ─── Implementation ──────────────────────────────────────────────────────

    public class DigitalSignService : IDigitalSignService
    {
        private readonly HttpClient               _http;
        private readonly ILogger<DigitalSignService> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public DigitalSignService(HttpClient http, ILogger<DigitalSignService> logger)
        {
            _http   = http;
            _logger = logger;
        }

        public async Task<DsSignResult?> SignDarAsync(
            string darNo,
            string signerSamAcc,
            string role,
            string purpose,
            string department,
            string? remarks = null)
        {
            try
            {
                var payload = new
                {
                    DataToSign      = darNo,           // ใช้ DarNo เป็นข้อมูลที่เซ็น
                    ReferenceId     = darNo,           // ใช้ DarNo เป็น reference สำหรับ audit
                    Purpose         = purpose,         // เช่น "Reviewer Approval"
                    Department      = department,
                    Remarks         = remarks ?? string.Empty,
                    signerUsername  = signerSamAcc     // SamAcc → API ใช้ find cert
                };

                var response = await _http.PostAsJsonAsync("/api/sign", payload);
                var body     = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[DigitalSign] API returned {status} for {darNo}: {body}",
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
    }
}
