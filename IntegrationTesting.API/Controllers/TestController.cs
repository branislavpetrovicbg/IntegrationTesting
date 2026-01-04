using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace IntegrationTesting.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TestController> _logger;

        public TestController(IConfiguration config, ILogger<TestController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetNameById(int id)
        {
            var connString = _config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connString))
                return Problem("Connection string 'DefaultConnection' is not configured.", statusCode: 500);

            try
            {
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("SELECT \"Name\" FROM \"Test\" WHERE \"Id\" = @id", conn);
                cmd.Parameters.AddWithValue("id", id);
                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value) return NotFound();
                return Ok(result.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying Test table for Id {Id}", id);
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }
    }
}
