using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.ViewModels.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers.Api;

[ApiController]
[Route("api/services")]
[Authorize(Policy = RoleNames.AllRoles)]
public class ServicesApiController : ControllerBase
{
    private readonly PetCareHubContext _context;

    public ServicesApiController(PetCareHubContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _context.Services.Include(x => x.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.ServiceName.Contains(keyword));
        }

        var paged = await PaginatedList<Service>.CreateAsync(query.OrderBy(x => x.ServiceName), page, pageSize);
        return Ok(new
        {
            paged.PageIndex,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
            items = paged.Items.Select(x => new
            {
                x.ServiceId,
                x.ServiceName,
                x.Price,
                x.DurationMinutes,
                x.Status,
                Category = x.Category.CategoryName
            })
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var service = await _context.Services.Include(x => x.Category).FirstOrDefaultAsync(x => x.ServiceId == id);
        return service is null ? NotFound() : Ok(service);
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Create(ServiceApiRequest request)
    {
        var service = new Service
        {
            CategoryId = request.CategoryId,
            ServiceName = request.ServiceName,
            Description = request.Description,
            Price = request.Price,
            DurationMinutes = request.DurationMinutes,
            Status = request.Status
        };

        _context.Services.Add(service);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = service.ServiceId }, service);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Update(int id, ServiceApiRequest request)
    {
        var service = await _context.Services.FindAsync(id);
        if (service is null)
        {
            return NotFound();
        }

        service.CategoryId = request.CategoryId;
        service.ServiceName = request.ServiceName;
        service.Description = request.Description;
        service.Price = request.Price;
        service.DurationMinutes = request.DurationMinutes;
        service.Status = request.Status;
        await _context.SaveChangesAsync();
        return Ok(service);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await _context.Services.FindAsync(id);
        if (service is null)
        {
            return NotFound();
        }

        _context.Services.Remove(service);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
