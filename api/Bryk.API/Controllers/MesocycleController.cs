using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;

namespace Bryk.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MesocycleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MesocycleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Mesocycle
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Mesocycle>>> GetMesocycles()
        {
            return await _context.Mesocycles.ToListAsync();
        }

        // GET: api/Mesocycle/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Mesocycle>> GetMesocycle(Guid id)
        {
            var mesocycle = await _context.Mesocycles.FindAsync(id);

            if (mesocycle == null)
            {
                return NotFound();
            }

            return mesocycle;
        }

        // PUT: api/Mesocycle/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMesocycle(Guid id, Mesocycle mesocycle)
        {
            if (id != mesocycle.Id)
            {
                return BadRequest();
            }

            _context.Entry(mesocycle).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MesocycleExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Mesocycle
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Mesocycle>> PostMesocycle(Mesocycle mesocycle)
        {
            _context.Mesocycles.Add(mesocycle);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMesocycle", new { id = mesocycle.Id }, mesocycle);
        }

        // DELETE: api/Mesocycle/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMesocycle(Guid id)
        {
            var mesocycle = await _context.Mesocycles.FindAsync(id);
            if (mesocycle == null)
            {
                return NotFound();
            }

            _context.Mesocycles.Remove(mesocycle);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MesocycleExists(Guid id)
        {
            return _context.Mesocycles.Any(e => e.Id == id);
        }
    }
}
