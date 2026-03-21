using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("dev")]
public class DevEmailController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly IHostEnvironment _hostEnv;
    public DevEmailController(IWebHostEnvironment env, IHostEnvironment hostEnv)
    {
        _env = env;
        _hostEnv = hostEnv;
    }

    // GET /dev/mails
    [HttpGet("mails")]
    public IActionResult Index()
    {
        if (!_hostEnv.IsDevelopment()) return NotFound();

        var dir = Path.Combine(_env.ContentRootPath, "dev-mails");
        if (!Directory.Exists(dir)) return NotFound("dev-mails folder not found");

        var files = Directory.GetFiles(dir)
            .Select(f => Path.GetFileName(f))
            .OrderByDescending(n => n)
            .ToList();

        return View(files); // now Model is IEnumerable<string>
    }

    // GET /dev/mails/view?name=...
    [HttpGet("mails/view")]
    public IActionResult ViewMail(string name)
    {
        if (!_hostEnv.IsDevelopment()) return NotFound();
        if (string.IsNullOrEmpty(name)) return BadRequest();

        var dir = Path.Combine(_env.ContentRootPath, "dev-mails");
        var safe = Path.GetFileName(name); // prevent traversal
        var full = Path.Combine(dir, safe);
        if (!System.IO.File.Exists(full)) return NotFound();

        var html = System.IO.File.ReadAllText(full);
        // return as HTML. This outputs the actual email HTML file unmodified.
        return Content(html, "text/html");
    }

    // GET /dev/mails/raw?name=...
    [HttpGet("mails/raw")]
    public IActionResult Raw(string name)
    {
        if (!_hostEnv.IsDevelopment()) return NotFound();
        if (string.IsNullOrEmpty(name)) return BadRequest();

        var dir = Path.Combine(_env.ContentRootPath, "dev-mails");
        var safe = Path.GetFileName(name);
        var full = Path.Combine(dir, safe);
        if (!System.IO.File.Exists(full)) return NotFound();

        var bytes = System.IO.File.ReadAllBytes(full);
        return File(bytes, "text/html", safe);
    }
}


