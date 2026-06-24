using Microsoft.AspNetCore.Mvc;
using SmartRFQ.API.Data;
using SmartRFQ.API.Models;
using Microsoft.EntityFrameworkCore;


namespace SmartRFQ.API.Controllers;


[ApiController]
[Route("api/doc-request")]
public class SapCodeController(AppDbContext db) : ControllerBase
{
     [HttpGet("sap-item")]
      public async Task<ActionResult<IEnumerable<SapCodeDto>>> GetSapCode()
      {
        var result = await db.SapCodes
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.Code)
            .Select(g => new SapCodeDto
            {
                Code = g.Code,
                Description = g.Description
            })
            .ToListAsync();
 
        return Ok(result);
      }
}

public class SapCodeDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}