// src/Ailos.EncryptedId.TestApi/Controllers/ObfuscationController.cs
using Ailos.EncryptedId;
using Microsoft.AspNetCore.Mvc;

namespace Ailos.EncryptedId.TestApi.Controllers;

[ApiController]
[Route("api/obfuscation")]
public class ObfuscationController : ControllerBase
{
    private readonly IEncryptedIdService _service;

    public ObfuscationController()
    {
        // Agora vai funcionar porque o Program.cs já carregou o .env da raiz
        var secret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET")
            ?? throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurada. Verifique o arquivo .env na raiz do projeto.");
        
        _service = EncryptedIdFactory.CreateService(secret);
    }

    [HttpGet("encrypt/{id:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult<object> Encrypt(long id)
    {
        var encrypted = _service.Encrypt(id);
        return Ok(new { value = encrypted.Value });
    }

    [HttpGet("decrypt/{value}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<object> Decrypt(string value)
    {
        try
        {
            var decrypted = _service.Decrypt(new EncryptedId(value));
            return Ok(new { id = decrypted });
        }
        catch (Exception ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Falha na descriptografia",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPost("batch-test")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult BatchTest([FromBody] long[] ids)
    {
        var results = new List<object>();
        
        foreach (var id in ids)
        {
            try
            {
                var encrypted = _service.Encrypt(id);
                var decrypted = _service.Decrypt(encrypted);
                var isValid = id == decrypted;
                
                results.Add(new
                {
                    OriginalId = id,
                    Encrypted = encrypted.Value,
                    Decrypted = decrypted,
                    IsValid = isValid
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    OriginalId = id,
                    Error = ex.Message
                });
            }
        }
        
        return Ok(results);
    }
}
