using Microsoft.AspNetCore.Mvc;
using SmartRFQ.API.Data;
using SmartRFQ.API.Models;
using Microsoft.EntityFrameworkCore;


namespace SmartRFQ.API.Controllers;


[ApiController]
[Route("api/doc-request")]
public class GlCodeController(AppDbContext db) : ControllerBase
{
     [HttpGet("gl-code")]
      public async Task<ActionResult<IEnumerable<GlCodeDto>>> GetGlCodes()
      {
        var result = await db.GLCodes
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.Code)
            .Select(g => new GlCodeDto
            {
                Code = g.Code,
                Description = g.Description
            })
            .ToListAsync();
 
        return Ok(result);
      }
}

public class GlCodeDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}