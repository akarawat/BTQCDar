using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net;
using Newtonsoft.Json;
using BTQCDar.Models;

namespace BTQCDar.Controllers;

public class SendMailController : Controller
{
    [HttpPost]
    public async Task<object> MailSenderMessage([FromBody] SenderMailModel obj)
    {
        IConfiguration _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        string strMsg   = "Success";
        string mailForm = _configuration[key: "TBCorApiServices:MailForm"];
        string MailDebug= _configuration[key: "TBCorApiServices:MailDebug"];

        // ── Start time (for log) ───────────────────────────────────────
        var now = DateTime.Now;

        if (MailDebug == "1")
        {
            strMsg = "Debug Mode: " + obj?.Body + " | " + obj?.Subject + " | " + obj?.Addresses;
            WriteLog(now, obj, strMsg, isDebug: true);
            return Ok(strMsg);
        }

        // ── Send actual Email ───────────────────────────────────────────────
        try
        {
            SenderMailModel param = new SenderMailModel
            {
                Body      = obj.Body,
                Form      = mailForm,
                Subject   = obj.Subject,
                Addresses = obj.Addresses,
                Priority  = 1
            };

            StringContent strContent = new StringContent(
                JsonConvert.SerializeObject(param),
                System.Text.Encoding.UTF8,
                "application/json");

            CookieContainer container = new CookieContainer();
            var handler1 = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer        = container,
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using (var httpClient = new HttpClient(handler1))
            {
                string baseUrl  = _configuration[key: "TBCorApiServices:EmailSender"];
                var    tokenNo  = "-dev_token-";
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + tokenNo);
                httpClient.DefaultRequestHeaders.Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (var response = await httpClient.PostAsync(baseUrl, strContent))
                {
                    strMsg = await response.Content.ReadAsStringAsync();
                }
            }
        }
        catch (Exception ex)
        {
            strMsg = "ERROR: " + ex.Message;
        }

        // ── Write Log ─────────────────────────────────────────────────
        WriteLog(now, obj, strMsg, isDebug: false);

        return Ok(strMsg);
    }

    // ────────────────────────────────────────────────────────────────────
    //  WriteLog — write log to file in logs/ folder
    //  Filename: mail_log_yyyy-MM-dd.txt (one file per day)
    // ────────────────────────────────────────────────────────────────────
    private void WriteLog(DateTime logTime, SenderMailModel? obj,
                          string result, bool isDebug)
    {
        try
        {
            // logs folder is in App root (same as stdout log)
            string logDir  = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            Directory.CreateDirectory(logDir);   // Create folder if not exists

            // Filename per day
            string logFile = Path.Combine(logDir,
                $"mail_log_{logTime:yyyy-MM-dd}.txt");

            // Log entry content
            string entry = string.Join(Environment.NewLine, new[]
            {
                "─────────────────────────────────────────────",
                $"[{logTime:yyyy-MM-dd HH:mm:ss}]",
                $"Mode    : {(isDebug ? "DEBUG" : "PRODUCTION")}",
                $"To      : {obj?.Addresses ?? "(null)"}",
                $"Subject : {obj?.Subject   ?? "(null)"}",
                $"From    : {obj?.Form      ?? "(null)"}",
                $"Result  : {result}",
                ""
            });

            // Append to file
            System.IO.File.AppendAllText(logFile, entry,
                System.Text.Encoding.UTF8);
        }
        catch
        {
            // If log write fails → does not affect main flow
        }
    }
}
